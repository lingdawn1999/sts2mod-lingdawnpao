using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪场（玩家增益，由「雪场」卡赋予，Counter 可叠加）：
    /// **在你的回合开始时，把等同层数的「雪球」加入手牌**（层数固定 1；升级换成「大雪球」）。
    /// 冰球线的自动供弹器——每回合免费来几张 0 费挂冰牌，喂「狂欢时刻」的格挡、「雪球发射器」的弹仓。
    /// 用 AfterSideTurnStart 而不是 BeforeSideTurnStart（照本体 §SideTurnStart 注释里的口径：
    /// 涉及"获得资源"的用 After，避免和起手抽牌 / 回合开始结算抢顺序）。
    /// <para>
    /// **2026-09-25 用户改升级位**：升级不再加层数，改成把供弹的「雪球」换成「大雪球」。
    /// 具体走 <see cref="SpawnsBig" /> 这个旗标——由卡的 `OnPlay` 在挂能力时按 `IsUpgraded` 写入。
    /// 之所以不用"层数"表达：`StackType` 是 Counter，层数会随重复打出叠加，
    /// 拿层数兼职"质量标记"会让"打两张普通雪场"叠出 2 张普通雪球而不是 1 张，语义会乱。
    /// </para>
    /// </summary>
    public sealed class SnowfieldPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>供弹是否换成「大雪球」（升级版雪场写入；一旦写入不会再退回普通雪球）。</summary>
        public bool SpawnsBig { get; set; }

        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner) || base.Amount <= 0m)
            {
                return;
            }

            Player? owner = base.Owner.Player;
            if (owner == null)
            {
                return;
            }

            if (SpawnsBig)
            {
                await BigSnowball.CreateIntoHandAsync(owner, (int)base.Amount, combatState);
            }
            else
            {
                await Snowball.CreateIntoHandAsync(owner, (int)base.Amount, combatState);
            }
        }
    }
}