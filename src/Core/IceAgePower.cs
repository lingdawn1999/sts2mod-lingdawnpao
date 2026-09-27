using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜天（玩家增益，由「霜天」金卡赋予，Counter 可叠加）：
    /// **在你的回合开始时，给予所有敌人等同层数的冰冻**（基础 5，升级 7）。
    /// 冰冻线的自动引擎——每回合免费推进全场层数，喂「雪崩」「深冬」「冰壳」。
    /// 走 `FrostbitePower.ApplyFrostAsync`（applier = 玩家），所以「极寒」「雪隐」「霜之护符」
    /// 都会被这次挂冰触发。
    /// **先对 `HittableEnemies` 取快照再遍历**：挂冰伤害会打死敌人，直接遍历活体集合会在中途被改。
    /// </summary>
    public sealed class IceAgePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner))
            {
                return;
            }

            // 走批量入口：「雪隐」的格挡回调整批只触发一次，不按敌人数放大
            await FrostbitePower.ApplyFrostToAllAsync(new ThrowingPlayerChoiceContext(),
                combatState.HittableEnemies.ToList(), base.Amount, base.Owner, null);
        }
    }
}