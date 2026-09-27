using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻（玩家增益，由「狂欢时刻」卡赋予，Counter 可叠加）：**被动标记能力**，自己不挂钩子。
    /// 语义 = 「雪球」「大雪球」造成的[gold]冰冻[/gold]层数额外 +层数（基础 1、升级 2）。
    /// 判定与加算统一在 <see cref="FrostbitePower.ApplyFrostAsync" /> 里（照「凝寒」`FrostIntensifyPower`
    /// 的同一行写法），只认 `cardSource` 是雪球/大雪球——所以「雪球发射器」「打雪仗」「雪场」
    /// 这些**自动打出雪球**的来源一样吃加成（它们最终都是"打出雪球牌"）。
    /// **2026-09-23 用户重做**：原效果是"每当你打出雪球/大雪球，获得等同层数的**格挡**"（供弹即防御），
    /// 改成直接放大雪球的挂冰层数——雪球线的收益从"防御"挪到"冰冻强度"，与「凝寒」形成"全局 +N"与
    /// "仅雪球 +N"的两档分工。
    /// </summary>
    public sealed class CarnivalMomentPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}