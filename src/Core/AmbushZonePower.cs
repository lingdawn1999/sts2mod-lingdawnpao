using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击区域的能力化显示：层数 = 当前埋伏的牌数量。
    /// 纯提示用，不携带行为；区域加减牌时由 AmbushZone 同步 +/-1 层。
    /// </summary>
    public sealed class AmbushZonePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public void Pulse() => Flash();
    }
}
