using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 凝寒（玩家增益，由「凝寒」金卡赋予）：
    /// 你**每次造成冰冻**时，本次施加的层数额外 +Amount。
    /// 实际结算在 <see cref="FrostbitePower.ApplyFrostAsync" /> 里读施加者身上的层数加算，
    /// 所以「霜天」「雪场」这类自动挂冰来源同样吃加成。
    /// 注意：只对 `applier != null` 的挂冰生效——「冰封王座」把冰冻挂到玩家自己身上时 applier 为空，不吃加成。
    /// </summary>
    public sealed class FrostIntensifyPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}