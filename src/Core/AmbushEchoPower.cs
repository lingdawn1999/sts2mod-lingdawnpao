using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏兵（玩家增益，由「伏兵」金卡赋予，Counter 可叠加）：
    /// 每层使「埋伏」的第二层效果额外触发一次。抓到多张能力牌可叠加多层。
    /// 与冰冻线的 FrozenAdeptPower（冰封）完全同型，两张分别是各自机制的"倍增件"。
    /// </summary>
    public sealed class AmbushEchoPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>埋伏效果的额外触发次数（= 能力层数）。</summary>
        public int ExtraTriggers => (int)Amount;
    }
}