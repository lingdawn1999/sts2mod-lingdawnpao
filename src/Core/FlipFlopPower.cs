using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 反复横跳（玩家增益，由「反复横跳」能力牌赋予）：伏击区域的牌数**每变化 1 张**
    /// （埋入增加、带出/消耗减少都算），就获得等同层数的格挡。
    /// 实际结算由 AmbushZone.NotifyCountChangedAsync 在区域增删处调用（每处会传入变化量，
    /// 格挡 = |变化张数| × 层数）。
    /// <para>
    /// **2026-09-24 用户定案：技能牌 → 能力牌**，本能力随之从"仅当回合有效"改为**整场战斗持续**
    /// （原先覆写了 `AfterSideTurnEnd` 在玩家回合结束时自删，现已移除）。改动缘由见
    /// <see cref="FlipFlop" /> 的类注释。
    /// </para>
    /// </summary>
    public sealed class FlipFlopPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}