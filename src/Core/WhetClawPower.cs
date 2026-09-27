using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 磨爪（玩家增益，由「磨爪」卡赋予，Counter 可叠加）：
    /// 层数 = 本场战斗中所有「磨爪」同名牌的伤害加值，累加。
    /// 单张「磨爪」自己结算时不享受它刚加的那一份（先打伤害、再加值，避免自己吃自己）。
    /// 显示为可见增益，让玩家能看到当前加成总量；埋伏（跨回合潜伏后带出）会让这张牌**额外打出一次**，
    /// 也就是同一段效果连做两遍、加值自然也是两份（口径见 <see cref="WhetClaw" /> 的类注释）。
    /// </summary>
    public sealed class WhetClawPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;
    }
}