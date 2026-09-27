using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 手牌上限宽限族（姿态牌在手时 +1 格）。四个类必须一起看：
    /// <para>
    /// ⚠️ **为什么是 IL 转译而不是普通 detour**：<c>CardPile.MaxCardsInHand</c> 是只有两行 IL 的常量属性，
    /// 会被 CoreCLR JIT 内联进调用方，普通 Prefix detour 直接被绕过（本项目 2026-09 在
    /// <c>CardModel.TryManualPlay</c> 上实测过这个坑）。<see cref="HandLimitTranspiler" /> 改的是
    /// **调用方自己的 IL**——把 <c>MaxCardsInHand</c> 的 getter 调用点替换成
    /// <see cref="AmbushStance.HandLimit" />（含宽限逻辑），无论调用方怎么内联都绕不过去。
    /// 目标方法都是 async 方法，真正的 IL 在编译器生成的状态机 MoveNext 里——
    /// <see cref="HandLimitTranspiler.StateMachineMoveNext" /> 负责按 AsyncStateMachineAttribute 找到它。
    /// </para>
    /// <para>
    /// ⚠️ **宽限判据刻意是"任意持有者手牌里有姿态牌"**（<see cref="AmbushStance.HandLimit" />）：
    /// MaxCardsInHand 是无参静态属性，调用点拿不到"这是谁的手牌"；按本地玩家判定联机两端会脱同步，
    /// 按"本场战斗有没有人持有姿态牌"则两端一致。代价：混编联机里非雪豹角色也多 1 格——不脱同步优先。
    /// </para>
    /// <para>
    /// <see cref="HandLimitBridge" />：抽牌路径的**二次改接**——<c>CardPileCmd.Draw</c> 里"手牌是否已满"
    /// 的判定走的不是 MaxCardsInHand 而是本体自己的
    /// <c>CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot</c>（内含硬编码的旧上限），转译器把它改接到
    /// <see cref="HandLimitBridge.CheckDrawPossible" />：无宽限/抽牌堆与弃牌堆全空时回落本体检查，
    /// 有宽限且未达 <see cref="AmbushStance.HandLimit" /> 时放行，超限弹"手牌已满"气泡并拒绝。
    /// </para>
    /// </summary>
    internal static class HandLimitBridge
    {
        private static readonly MethodInfo? VanillaCheckMethod = AccessTools.Method(
            typeof(CardPileCmd), "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot");

        private static readonly Func<Player, bool>? VanillaCheck =
            VanillaCheckMethod?.CreateDelegate<Func<Player, bool>>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool CheckDrawPossible(Player player)
        {
            // 本体检查器找不到（签名变更）→ 放行让本体自己兜底，宁多抽不可崩
            if (VanillaCheck == null)
            {
                return true;
            }

            // 无宽限（场上没有任何人持有姿态牌）→ 走本体原判定
            if (AmbushStance.HandLimit == CardPile.MaxCardsInHand)
            {
                return VanillaCheck(player);
            }

            // 抽牌堆和弃牌堆都空（回合末洗牌等情况）→ 本体原判定
            if (PileType.Draw.GetPile(player).Cards.Count + PileType.Discard.GetPile(player).Cards.Count == 0)
            {
                return VanillaCheck(player);
            }

            // 达到宽限后上限 → 弹"手牌已满"气泡并拒绝
            if (PileType.Hand.GetPile(player).Cards.Count >= AmbushStance.HandLimit)
            {
                ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), player.Creature, 2.0);
                return false;
            }

            return true;
        }
    }

    /// <summary>转译器工具箱：MaxCardsInHand→HandLimit 替换 + 抽牌检查改接 + 状态机 MoveNext 定位。</summary>
    internal static class HandLimitTranspiler
    {
        private static readonly MethodInfo? MaxGetter = AccessTools.PropertyGetter(typeof(CardPile), "MaxCardsInHand");

        private static readonly MethodInfo? HandLimitGetter = AccessTools.PropertyGetter(typeof(AmbushStance), "HandLimit");

        private static readonly MethodInfo? DrawPossibleHelper =
            AccessTools.Method(typeof(CardPileCmd), "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot");

        /// <summary>async 方法的 IL 在编译器生成的状态机 MoveNext 里——按 AsyncStateMachineAttribute 定位。</summary>
        internal static MethodBase StateMachineMoveNext(MethodInfo stub, string patchName)
        {
            AsyncStateMachineAttribute? attr = stub.GetCustomAttribute<AsyncStateMachineAttribute>();
            if (attr?.StateMachineType == null)
            {
                throw new MissingMemberException(patchName + "：" + stub.Name + " 上没有 AsyncStateMachineAttribute，取不到状态机类型");
            }

            MethodInfo? moveNext = attr.StateMachineType.GetMethod(
                "MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            moveNext ??= attr.StateMachineType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.EndsWith("IAsyncStateMachine.MoveNext", StringComparison.Ordinal));
            return moveNext ?? throw new MissingMemberException(patchName + "：" + attr.StateMachineType.Name + " 里找不到 MoveNext");
        }

        /// <summary>抽牌路径：MaxCardsInHand 替换 + 抽牌检查改接（两处都要）。</summary>
        internal static IEnumerable<CodeInstruction> TranspileDraw(IEnumerable<CodeInstruction> instructions, string patchName)
        {
            List<CodeInstruction> list = instructions.ToList();
            int replaced = ReplaceGetterCalls(list, patchName);
            int redirected = RedirectDrawPossibleCalls(list, patchName);
            Log.Info($"芝士雪豹：「{patchName}」手牌上限调用点替换 {replaced} 处，抽牌检查改接 {redirected} 处", 2);
            return list;
        }

        /// <summary>埋牌路径：只做 MaxCardsInHand 替换。</summary>
        internal static IEnumerable<CodeInstruction> TranspileAdd(IEnumerable<CodeInstruction> instructions, string patchName)
        {
            List<CodeInstruction> list = instructions.ToList();
            int replaced = ReplaceGetterCalls(list, patchName);
            Log.Info($"芝士雪豹：「{patchName}」手牌上限调用点替换 {replaced} 处", 2);
            return list;
        }

        private static int ReplaceGetterCalls(List<CodeInstruction> instructions, string patchName)
        {
            if (MaxGetter == null || HandLimitGetter == null)
            {
                throw new MissingMemberException("手牌上限转译失败：找不到 MaxCardsInHand 或 HandLimit");
            }

            int count = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method == MaxGetter)
                {
                    instruction.operand = HandLimitGetter;
                    count++;
                }
            }

            if (count == 0)
            {
                Log.Warn("芝士雪豹：「" + patchName + "」里没找到 CardPile.MaxCardsInHand 调用点，该处手牌上限豁免未生效（本体可能改过实现）", 2);
            }

            return count;
        }

        private static int RedirectDrawPossibleCalls(List<CodeInstruction> instructions, string patchName)
        {
            MethodInfo? bridge = AccessTools.Method(typeof(HandLimitBridge), "CheckDrawPossible");
            if (DrawPossibleHelper == null || bridge == null)
            {
                throw new MissingMemberException("抽牌检查改接失败：找不到 CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot 或 CheckDrawPossible");
            }

            int count = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && method == DrawPossibleHelper)
                {
                    instruction.operand = bridge;
                    count++;
                }
            }

            if (count == 0)
            {
                Log.Warn("芝士雪豹：「" + patchName + "」里没找到 CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot 调用点，手牌满时可能仍然抽不出牌", 2);
            }

            return count;
        }
    }

    /// <summary>埋牌路径（CardPileCmd.Add）的手牌上限转译。</summary>
    [HarmonyPatch]
    internal static class CardPileCmd_Add_HandLimitPatch
    {
        private static MethodBase TargetMethod()
        {
            return HandLimitTranspiler.StateMachineMoveNext(
                AccessTools.Method(typeof(CardPileCmd), "Add", new[]
                {
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool),
                }) ?? throw new MissingMethodException("找不到 CardPileCmd.Add(cards, pile, position, clonedBy, skipVisuals)"),
                "CardPileCmd_Add_HandLimitPatch");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            HandLimitTranspiler.TranspileAdd(instructions, "CardPileCmd_Add_HandLimitPatch");
    }

    /// <summary>抽牌路径（CardPileCmd.Draw）的手牌上限转译 + 抽牌检查改接。</summary>
    [HarmonyPatch]
    internal static class CardPileCmd_Draw_HandLimitPatch
    {
        private static MethodBase TargetMethod()
        {
            return HandLimitTranspiler.StateMachineMoveNext(
                AccessTools.Method(typeof(CardPileCmd), "Draw", new[]
                {
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool),
                }) ?? throw new MissingMethodException("找不到 CardPileCmd.Draw(choiceContext, count, player, fromHandDraw)"),
                "CardPileCmd_Draw_HandLimitPatch");
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            HandLimitTranspiler.TranspileDraw(instructions, "CardPileCmd_Draw_HandLimitPatch");
    }
}
