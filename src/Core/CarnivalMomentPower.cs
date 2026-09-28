using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻（玩家增益，由「狂欢时刻」卡赋予，Counter 可叠加）：
    /// Amount = **雪球/大雪球造成的冰冻层数加成**（打出卡时 1，升级 +1 → 2；重复打出继续叠）。
    /// 加算收口在 <see cref="FrostbitePower.ApplyFrostAsync" />（只认雪球系 cardSource，
    /// 自动打出雪球的来源——发射器/打雪仗/雪场——同样吃加成；自挂冰 applier=null 不吃）；
    /// 溅射在 <see cref="Snowball.OnPlay" /> / <see cref="BigSnowball.OnPlay" />：
    /// 场上有本能力时，雪球的挂冰改走 <see cref="FrostbitePower.ApplyFrostToAllAsync" />。
    /// 与「凝寒」的回合内滚动（FrostIntensifyPower）相互独立、同时生效。
    /// </summary>
    public sealed class CarnivalMomentPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}
