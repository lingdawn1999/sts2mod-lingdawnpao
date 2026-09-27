using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻（玩家增益，由「狂欢时刻」卡赋予，Counter 可叠加）：**被动标记能力**，自己不挂钩子。
    /// **2026-09-27 用户重做（原"雪球挂冰+固定层数"评定为烂卡）**：语义 = 「雪球」「大雪球」的挂冰
    /// **溅射全体敌人**（Amount=1 纯开关、加成 0；升级 Amount=2 → 全体层数 +1）。
    /// 判定与溅射统一在 <see cref="FrostbitePower.ApplyFrostAsync" />（cardSource 是雪球/大雪球 &&
    /// 顶层施加时改走批量入口），只认雪球系 cardSource——所以「雪球发射器」「打雪仗」「雪场」
    /// 这些**自动打出雪球**的来源一样吃溅射。
    /// </summary>
    public sealed class CarnivalMomentPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}