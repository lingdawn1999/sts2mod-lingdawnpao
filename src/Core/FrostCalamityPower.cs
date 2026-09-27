using System;
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
    /// 寒灾（玩家增益，由「寒灾」卡赋予）：
    /// **任何带有冰冻的敌人死亡时，其余存活敌人各获得其剩余层数的一半**（向下取整，不足 2 层不发遗产）。
    /// 用 BeforeDeath 而不是 AfterDeath：死亡结算走完时目标身上的能力已被移除，取不到冰冻层数。
    /// 「遗产」挂冰照常走 ApplyFrostAsync（applier = 玩家），所以这次传染同样吃「凝寒」的加层、
    /// 同样触发「雪隐」给格挡、同样会结算挂冰伤害——可能因此连锁打死下一个敌人，
    /// 所以每轮都重新判存活、并对存活敌人取快照再遍历（同「霜原」「霜天」的写法）。
    /// 递归安全性：每个敌人在一次死亡里只触发一次本钩子，不存在 A→B→A 的死循环。
    /// </summary>
    public sealed class FrostCalamityPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override async Task BeforeDeath(Creature target)
        {
            if (target == base.Owner || !base.Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            // 只对"敌人"发遗产：`Player` 非空说明是我方角色（多人局里也包括队友）
            if (target.Player != null)
            {
                return;
            }

            FrostbitePower? frost = target.GetPower<FrostbitePower>();
            if (frost == null)
            {
                return;
            }

            decimal heritage = Math.Floor(frost.Amount / 2m);
            if (heritage <= 0m)
            {
                return;
            }

            Flash();

            // 走批量入口：传染给多个敌人时，「雪隐」的格挡回调也只触发一次
            await FrostbitePower.ApplyFrostToAllAsync(new ThrowingPlayerChoiceContext(),
                base.CombatState.HittableEnemies.Where(e => e != target).ToList(), heritage, base.Owner, null);
        }
    }
}