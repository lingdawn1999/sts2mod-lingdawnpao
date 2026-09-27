using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 永恒狩猎（玩家增益，由「永恒狩猎」金卡赋予，Counter 可叠加）：
    /// **每回合你的入区额度 +等同层数 张**（<see cref="AmbushZoneState.QuotaLeft" /> 读取）。
    /// v2 里它只是"额度放大器"——改道结算统一在 <see cref="AmbushZone.TryDivertAfterPlayAsync" />。
    /// </summary>
    public sealed class EternalHuntPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}
