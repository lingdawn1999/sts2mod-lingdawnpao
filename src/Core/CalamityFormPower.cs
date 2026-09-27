using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狩猎形态（玩家增益，由「狩猎形态」金卡赋予）。形态牌本体不改数值，只做两件事：
    /// ① 「你的攻击无视格挡」——由 Patches 里的 CreatureCmd.Damage 前缀给玩家的攻击伤害叠加
    ///    ValueProp.Unblockable，打出当回合立刻生效。
    /// ② 「敌人受到的伤害增加」——**不做成玩家侧乘区**，而是每回合给所有敌人挂 1 层「破绽」
    ///    （用户定案）：这样人工制品会先吃掉这次施加，人工制品层数没削掉时吃不到加成；
    ///    与本体 DemonForm 一样，形态的收益从**下个回合开始**（打出当回合不挂破绽）。
    /// </summary>
    public sealed class CalamityFormPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner))
            {
                return;
            }

            foreach (Creature enemy in combatState.HittableEnemies)
            {
                await PowerCmd.Apply<CalamityMarkPower>(new ThrowingPlayerChoiceContext(), enemy, 1m, base.Owner, null);
            }
        }
    }
}