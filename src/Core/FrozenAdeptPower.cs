using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜冻回响：由能力牌赋予的常驻玩家增益，Counter可叠加。
    /// 每层使冰冻伤害额外结算一次，不在回合结束时移除。
    /// </summary>
    public sealed class FrozenAdeptPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>挂冰伤害的额外触发次数（= 能力层数）。</summary>
        public int ExtraTriggers => (int)Amount;

    }
}
