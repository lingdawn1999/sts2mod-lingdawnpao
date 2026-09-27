using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// **本体"写死 6 个本体角色"的兼容补丁合集**（2026-09-23）。
    /// <para>
    /// 本体的存档/解锁/统计/图鉴这几层大量假设"角色集合就是那 5 个本体角色"：用角色当字典键、
    /// 按角色拼纪元 id、遍历本体角色列表。自制角色一头撞进去就是**抛异常**，而其中一部分抛在
    /// **战斗结算的异步链**里——异常一抛，后面创建奖励界面的代码就再也不会执行，
    /// 玩家看到的就是"打完精英/boss 卡住、没有奖励"（2026-09-23 用户报障的那类问题）。
    /// </para>
    /// <para>
    /// **为什么不用 try/catch 硬吞**：吞异常会掩盖本体逻辑只做了一半的中间态（本体注释也明确警告过）。
    /// 这里一律用 **Prefix 返回 false 精确跳过整段**，并且只在"当前玩家角色是芝士雪豹"时生效，
    /// 不影响本体角色的任何行为。
    /// </para>
    /// <para>
    /// **参考**：同类社区做法见基座库 `STS2-RitsuLib` 的 `Unlocks.Patches`
    /// （`EliteEpochCompatibilityPatch` / `BossEpochCompatibilityPatch` 也是 Prefix 跳过，
    /// 并配套 `ModUnlockRegistry` 动态规则表）。我们目前不挂基座，所以自研这几条（详见会话记录）。
    /// </para>
    /// </summary>
    internal static class LingdawnpaoCompat
    {
        /// <summary>卡牌百科里我们自己注入的筛选按钮（由 <see cref="NCardLibrary_LingdawnpaoFilterPatch" /> 写入）。</summary>
        internal static NCardPoolFilter? OurCardPoolFilter { get; set; }

        /// <summary>当前玩家角色是不是芝士雪豹。</summary>
        internal static bool IsLingdawnpao(object? character) => character is Lingdawnpao;
    }

    /// <summary>
    /// **（最高优先级）boss 击杀解锁纪元**：<c>ProgressSaveManager.ObtainCharUnlockEpoch</c>
    /// 在 `RoomType.Boss` 分支里**先于** `CheckFifteenBossesDefeatedEpoch` 被调用，
    /// 它按角色拼出 `角色Id + "2_EPOCH"` 再交给 `EpochModel.Get`——而
    /// `EpochModel.Get` 对未知 id 是**直接 throw ArgumentException**（不是返回 null）。
    /// 自制角色没有这套纪元 → 每次打 boss 都抛，异常发生在
    /// `CombatManager.EndCombatInternal` 的异步链里 → **奖励界面不创建 = 打完 boss 卡死**。
    /// <para>
    /// 注意：这条**不在**之前那两个 `CheckFifteen*` 补丁的覆盖范围内（那两条是"十五次精英/boss"里程碑，
    /// 本方法在它们之前执行），所以必须单独补。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(ProgressSaveManager), "ObtainCharUnlockEpoch")]
    internal static class ProgressSaveManager_ObtainCharUnlockEpoch_LingdawnpaoPatch
    {
        private static bool Prefix(Player localPlayer)
        {
            if (!LingdawnpaoCompat.IsLingdawnpao(localPlayer.Character))
            {
                return true;
            }

            Log.Info("兼容：芝士雪豹没有对应的纪元解锁表，跳过 ObtainCharUnlockEpoch（否则打完 boss 会中断结算）");
            return false;
        }
    }

    /// <summary>
    /// **卡牌百科（牌库图鉴）打开即崩**：<c>NCardLibrary.OnSubmenuOpened</c> 里有
    /// <c>_cardPoolFilters[characterModel]</c>，而 `_cardPoolFilters` 是场景里按**5 个本体角色**
    /// 预填的字典 → 玩雪豹时用角色实例索引会抛 `KeyNotFoundException`。
    /// <para>
    /// 修法：在那个索引之前，把"**运行时那一个角色实例** → 我们自己注入的筛选按钮"补进字典。
    /// **必须用运行时实例当键**（不能拿 `ModelDb.Character<Lingdawnpao>()` 顶替）——
    /// `AbstractModel` 没有重写 `Equals`，字典是引用相等语义，键必须是本体即将查询的那个对象。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(NCardLibrary), "OnSubmenuOpened")]
    internal static class NCardLibrary_OnSubmenuOpened_LingdawnpaoFilterPatch
    {
        private static void Prefix(NCardLibrary __instance)
        {
            try
            {
                if (LingdawnpaoCompat.OurCardPoolFilter == null)
                {
                    return;
                }

                var runState = (IRunState?)AccessTools.Field(typeof(NCardLibrary), "_runState")
                    .GetValue(__instance);
                CharacterModel? character = LocalContext.GetMe(runState)?.Character;
                if (!LingdawnpaoCompat.IsLingdawnpao(character))
                {
                    return;
                }

                var map = (Dictionary<CharacterModel, NCardPoolFilter>)AccessTools
                    .Field(typeof(NCardLibrary), "_cardPoolFilters").GetValue(__instance)!;
                if (!map.ContainsKey(character!))
                {
                    map[character!] = LingdawnpaoCompat.OurCardPoolFilter;
                    Log.Info("兼容：已把雪豹筛选按钮注册进卡牌百科的角色筛选表");
                }
            }
            catch (Exception e)
            {
                // 纯 UI 层，出错也不能让图鉴打不开
                Log.Info($"兼容：卡牌百科雪豹筛选注册失败，跳过（{e.Message}）");
            }
        }
    }

    /// <summary>
    /// **结算界面写徽章**：<c>NGameOverScreen.SaveBadgesToProgress</c> 直接
    /// `Progress.CharacterStats[角色.Id]`，字典里没有这个角色就抛。
    /// 正常跑完一局时前面的统计流程会建好该项，但"没打过一场战斗就结束/放弃"这类路径可能缺项。
    /// 修法：只补"建好那一项"，其余逻辑照旧（不跳过方法）。
    /// </summary>
    [HarmonyPatch(typeof(NGameOverScreen), "SaveBadgesToProgress")]
    internal static class NGameOverScreen_SaveBadgesToProgress_LingdawnpaoPatch
    {
        private static void Prefix(NGameOverScreen __instance)
        {
            try
            {
                var localPlayer = (MegaCrit.Sts2.Core.Entities.Players.Player?)AccessTools
                    .Field(typeof(NGameOverScreen), "_localPlayer").GetValue(__instance);
                if (!LingdawnpaoCompat.IsLingdawnpao(localPlayer?.Character))
                {
                    return;
                }

                // ProgressState.GetOrCreateCharacterStats 会为任意 ModelId 建项（本体自己的兜底路径）
                MegaCrit.Sts2.Core.Saves.ProgressState progress = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.Progress;
                progress.GetOrCreateCharacterStats(localPlayer!.Character.Id);
            }
            catch (Exception e)
            {
                Log.Info($"兼容：结算界面角色统计预建项失败，跳过（{e.Message}）");
            }
        }
    }
}