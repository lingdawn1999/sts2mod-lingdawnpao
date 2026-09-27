using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击姿态的**三层防护 + 形态显示 + 右键切换**补丁集（2026-09-23 埋伏体系大改配套）。
    /// 姿态牌（<see cref="AmbushStance" />）必须永远留在手牌、不能被选走/变形/洗走，具体三层：
    /// <para>
    /// ① **变形拦**（<see cref="CardCmd_Transform_AmbushStanceGuardPatch" />）：`CardCmd.Transform` 批量变形时
    ///    从批次中摘除姿态牌（Compact 等效果会按类型批量变形）；
    /// ② **牌堆拦**（<see cref="CardPileCmd_Add_AmbushStanceGuardPatch" />）：姿态牌拒绝进弃牌堆/消耗堆/打出堆
    ///    （Discard/Exhaust/Play 三堆），整批全是姿态牌则整批拒绝，混批则摘除后放行其余；
    /// ③ **选牌过滤**（<see cref="CardSelectCmd_FromHand_AmbushStanceFilterPatch" />）：所有从手牌发起的
    ///    选牌界面（弃置/变形/放回/消耗一张手牌等）里姿态牌不可见不可选。
    /// </para>
    /// <para>
    /// 另有两个**形态显示**挂点与一个**右键切换**入口：
    /// · <see cref="CardModel_GetDescriptionForPile_AmbushStanceFormPatch" />：一卡两形态——
    ///   姿态牌的卡面文本按当前形态整体替换（<see cref="AmbushStance.FormDescription" />）；
    /// · <see cref="NCard_UpdateVisuals_AmbushStanceFormPatch" />：卡面立绘随形态切换
    ///   （<see cref="AmbushStance.ApplyFormPortrait" />）；
    /// · <see cref="NHandCardHolder_OnMousePressed_AmbushStanceRightClickPatch" />：手牌区**右键**姿态牌
    ///   切换伏击形态（走遗物的 <see cref="FrostFang.ToggleAmbushMode" />，联机状态经 OnAmbushModeSynced 同步）。
    /// </para>
    /// <para>
    /// 全部挂点带 <see cref="PatchSwallower" /> 容错（Harmony Finalizer 参数必须是 `__exception`，
    /// 否则注册阶段抛 HarmonyException 被跳过——历史教训）；TargetMethod 按签名动态解析，
    /// 本体改签名时抛 MissingMethodException 被吞，不会炸全局补丁注册。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class CardCmd_Transform_AmbushStanceGuardPatch
    {
        [HarmonyFinalizer]
        private static Exception? Fin(Exception? __exception) =>
            PatchSwallower.Swallow(__exception, "CardCmd_Transform_AmbushStanceGuardPatch");

        // 按签名精确匹配 Transform(IEnumerable<CardTransformation>, Rng, CardPreviewStyle) 重载
        private static MethodBase TargetMethod()
        {
            MethodInfo? target = AccessTools.GetDeclaredMethods(typeof(CardCmd)).FirstOrDefault(m =>
                m.Name == "Transform"
                && m.GetParameters() is { Length: 3 } ps
                && ps[0].ParameterType == typeof(IEnumerable<CardTransformation>));
            return target ?? throw new MissingMethodException("找不到 CardCmd.Transform(IEnumerable<CardTransformation>, Rng, CardPreviewStyle)");
        }

        private static bool Prefix(ref IEnumerable<CardTransformation> transformations, ref Task<IEnumerable<CardPileAddResult>> __result)
        {
            List<CardTransformation> list = transformations.ToList();
            if (!list.Any(item => item.Original is AmbushStance))
            {
                return true;
            }

            int removed = list.RemoveAll(item => item.Original is AmbushStance);
            Log.Info($"伏击姿态：已从变形批次中摘除 {removed} 张（该批剩 {list.Count} 张）", 2);
            if (list.Count == 0)
            {
                __result = Task.FromResult(Array.Empty<CardPileAddResult>() as IEnumerable<CardPileAddResult>);
                return false;
            }

            transformations = list;
            return true;
        }
    }

    /// <summary>牌堆拦：姿态牌拒绝进弃牌堆 / 消耗堆 / 打出堆（保持在手牌区）。</summary>
    [HarmonyPatch]
    internal static class CardPileCmd_Add_AmbushStanceGuardPatch
    {
        [HarmonyFinalizer]
        private static Exception? Fin(Exception? __exception) =>
            PatchSwallower.Swallow(__exception, "CardPileCmd_Add_AmbushStanceGuardPatch");

        // 按签名精确匹配 Add(IEnumerable<CardModel>, CardPile, ...) 批量重载
        private static MethodBase TargetMethod()
        {
            MethodInfo? target = AccessTools.GetDeclaredMethods(typeof(CardPileCmd)).FirstOrDefault(m =>
                m.Name == "Add"
                && m.GetParameters() is { Length: >= 5 } ps
                && ps[0].ParameterType == typeof(IEnumerable<CardModel>)
                && ps[1].ParameterType == typeof(CardPile));
            return target ?? throw new MissingMethodException("找不到 CardPileCmd.Add(IEnumerable<CardModel>, CardPile, ...)");
        }

        private static bool Prefix(ref IEnumerable<CardModel> cards, CardPile newPile, ref Task<IReadOnlyList<CardPileAddResult>> __result)
        {
            if (newPile.Type is not (PileType.Discard or PileType.Exhaust or PileType.Play))
            {
                return true;
            }

            if (!cards.Any(card => card is AmbushStance))
            {
                return true;
            }

            List<CardModel> list = cards.ToList();
            if (list.Count == list.Count(card => card is AmbushStance))
            {
                // 整批都是姿态牌 → 整批拒绝（返回 success=false 的结果序列，调用方按失败处理）
                __result = Task.FromResult(list.Select(card => new CardPileAddResult
                {
                    cardAdded = card,
                    success = false,
                }).ToList() as IReadOnlyList<CardPileAddResult>);
                Log.Info($"伏击姿态：拒绝移入 {newPile.Type} 牌堆（保持在手牌区）", 2);
                return false;
            }

            cards = list.Where(card => card is not AmbushStance).ToList();
            Log.Info($"伏击姿态：已从 {newPile.Type} 的批量操作中摘除（该批 {list.Count} 张 → {cards.Count()} 张）", 2);
            return true;
        }
    }

    /// <summary>选牌过滤：手牌发起的所有选牌界面里姿态牌不可见不可选。</summary>
    [HarmonyPatch(typeof(CardSelectCmd), "FromHand")]
    internal static class CardSelectCmd_FromHand_AmbushStanceFilterPatch
    {
        [HarmonyFinalizer]
        private static Exception? Fin(Exception? __exception) =>
            PatchSwallower.Swallow(__exception, "CardSelectCmd_FromHand_AmbushStanceFilterPatch");

        private static void Prefix(ref Func<CardModel, bool>? filter)
        {
            Func<CardModel, bool>? original = filter;
            filter = card => AmbushStance.IsSelectable(card) && (original?.Invoke(card) ?? true);
        }
    }

    /// <summary>卡面文本按形态整体替换（一卡两形态，见 AmbushStance.FormDescription）。</summary>
    [HarmonyPatch]
    internal static class CardModel_GetDescriptionForPile_AmbushStanceFormPatch
    {
        [HarmonyFinalizer]
        private static Exception? Fin(Exception? __exception) =>
            PatchSwallower.Swallow(__exception, "CardModel_GetDescriptionForPile_AmbushStanceFormPatch");

        // GetDescriptionForPile(PileType, DescriptionPreviewType, Creature)——第二参是 CardModel 的私有内嵌枚举
        private static MethodBase TargetMethod()
        {
            Type previewType = AccessTools.Inner(typeof(CardModel), "DescriptionPreviewType")
                ?? throw new MissingMemberException("找不到 CardModel.DescriptionPreviewType（本体可能改过实现）");
            return AccessTools.Method(typeof(CardModel), "GetDescriptionForPile", new[]
            {
                typeof(PileType),
                previewType,
                typeof(Creature),
            }) ?? throw new MissingMemberException("找不到 CardModel.GetDescriptionForPile(PileType, DescriptionPreviewType, Creature)");
        }

        private static bool Prefix(CardModel __instance, ref string __result)
        {
            if (__instance is not AmbushStance stance)
            {
                return true;
            }

            __result = stance.FormDescription;
            return false;
        }
    }

    /// <summary>卡面立绘随形态切换。</summary>
    [HarmonyPatch(typeof(NCard), "UpdateVisuals")]
    internal static class NCard_UpdateVisuals_AmbushStanceFormPatch
    {
        [HarmonyFinalizer]
        private static Exception? Fin(Exception? __exception) =>
            PatchSwallower.Swallow(__exception, "NCard_UpdateVisuals_AmbushStanceFormPatch");

        private static void Postfix(NCard __instance)
        {
            if (__instance.Model is AmbushStance stance)
            {
                AmbushStance.ApplyFormPortrait(__instance, stance);
            }
        }
    }

    /// <summary>手牌区右键姿态牌 → 切换伏击形态（经遗物，联机由 OnAmbushModeSynced 同步）。</summary>
    [HarmonyPatch(typeof(NHandCardHolder), "OnMousePressed")]
    internal static class NHandCardHolder_OnMousePressed_AmbushStanceRightClickPatch
    {
        [HarmonyFinalizer]
        private static Exception? Fin(Exception? __exception) =>
            PatchSwallower.Swallow(__exception, "NHandCardHolder_OnMousePressed_AmbushStanceRightClickPatch");

        private static void Postfix(NHandCardHolder __instance, InputEvent inputEvent)
        {
            if (inputEvent is not InputEventMouseButton click
                || !click.Pressed
                || click.ButtonIndex != MouseButton.Right
                || __instance.CardModel is not AmbushStance stance)
            {
                return;
            }

            Player? owner = stance.Owner;
            if (owner != null)
            {
                owner.GetRelic<FrostFang>()?.ToggleAmbushMode();
            }
        }
    }
}
