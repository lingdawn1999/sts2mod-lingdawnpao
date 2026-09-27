using System.Collections.Generic;
using Godot;
using lingdawnpao.Core.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 芝士雪豹遗物池：初始遗物「伏击者」+ 它的上位「暗杀者」+ 收官批三件专属
    /// （暖绒·白 / 霜铃·蓝 / 雪球袋·金）。
    /// ⚠️ **所有遗物都必须登记在这里**，否则 `RelicModel.Pool` 会抛
    /// `InvalidOperationException: Sequence contains no matching element`
    /// —— 本体的实现是 `ModelDb.AllRelicPools.First(p => p.AllRelicIds.Contains(Id))`，**查不到就抛**。
    /// <para>
    /// **2026-09-23 实际踩到的后果**（玩家在「欧洛巴斯」事件卡死，对话显示了但选项永远不出来）：
    /// 暗杀者原先只靠该事件直接授予、没进池。当玩家选到本体先古遗物「欧洛巴斯之触」时，
    /// `TouchOfOrobas.UpgradedRelic` 的 setter 要读暗杀者的 `HoverTips`
    /// → `DynamicDescription` → `EnergyIconHelper.GetPrefix` → `RelicModel.Pool` → 抛异常；
    /// 异常顺着 `GenerateInitialOptions` 冒到 `EventRoom.EnterInternal`，事件房 setup 任务直接死掉。
    /// 修法就是把它登记进池（`伏击者 → 暗杀者` 的映射本身已有，见 `Patches.cs` 的
    /// `TouchOfOrobas_GetUpgradedStarterRelic_AssassinPatch`，那一段是对的）。
    /// </para>
    /// 与卡牌那边同源的规矩：**自研模型必须挂进对应的池**（卡牌是 `TokenCardPool`，遗物就是这里）。
    /// </summary>
    public sealed class LingdawnpaoRelicPool : RelicPoolModel
    {
        public override string EnergyColorName => "defect";

        public override Color LabOutlineColor => new Color("3EB3ED");

        protected override IEnumerable<RelicModel> GenerateAllRelics() =>
        [
            ModelDb.Relic<FrostFang>(), ModelDb.Relic<Assassin>(),
            ModelDb.Relic<CozyPelt>(), ModelDb.Relic<Frostbell>(), ModelDb.Relic<SnowballPouch>(),
        ];
    }
}
