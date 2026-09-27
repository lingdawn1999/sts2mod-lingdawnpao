using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狩猎图鉴（玩家增益，由「狩猎图鉴」金卡赋予）：**本场战斗你每首次兑现一种不同的牌，
    /// 图鉴卡的伤害 +4**。本能力只是"显示载体"（纯计数展示，无行为），
    /// 兑现种类记账在 <see cref="AmbushZoneState.CashedOutKinds" />（按卡牌 Id 去重，
    /// 由 <see cref="AmbushZone.BringOutFirstAsync" /> 在带出时写入）——
    /// 卡面动态数值直接读 <see cref="KindCount" />。
    /// <para>
    /// 对位酒狐「剑术大师」（每打出过 1 种剑牌 +8）：收集里程碑型兑现器——
    /// 封顶来自玩家兑现的**广度**而非上限字段（2026-09-26 v2 新增，替代退役的「雪崩」）。
    /// </para>
    /// </summary>
    public sealed class HuntAtlasPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>本场战斗已兑现的不同卡牌种类数（图鉴卡的动态伤害加成份数）。</summary>
        public static int KindCount(Player player) =>
            AmbushZone.For(player).CashedOutKinds.Count;
    }
}
