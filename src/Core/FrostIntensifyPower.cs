using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 凝寒（玩家增益，由「凝寒」金卡赋予，2026-09-27 用户重做为**成长型**）：
    /// Amount = **成长计数**——你每次造成冰冻时，本次施加的层数 +Amount，结算后 Amount +1
    /// （打出卡片时垫 1 层，即第 1 次挂冰 +1、第 2 次 +2……）。
    /// 实际结算在 <see cref="FrostbitePower.ApplyFrostAsync" /> 里（只在顶层施加计次，批量溅射整批 +1），
    /// 所以「霜天」「雪场」这类自动挂冰来源同样吃加成。
    /// 注意：只对 `applier != null` 的挂冰生效——「冰封王座」把冰冻挂到玩家自己身上时 applier 为空，不计数不加成。
    /// </summary>
    public sealed class FrostIntensifyPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}