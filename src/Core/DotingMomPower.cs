using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 护短的雪豹妈妈（玩家增益，由同名动物牌赋予，Counter 可叠加）：
    /// 受到攻击时对攻击者返还等同层数的伤害（基础 3、升级 5）。
    /// **结构整段照本体 `ThornsPower`**（[ThornsPower.cs](file:///d:/sts2mod/sts2mod-unciphered/src/Core/Models/Powers/ThornsPower.cs)）——
    /// 唯一差别是**自解时机**：本体荆棘永久，本能力在**敌方回合结束时**移除
    /// （即覆盖整个敌方回合，口径同「听桥」/本体「火焰屏障」的 `AfterSideTurnEnd` + `Owner.Side != side`）。
    /// 反伤走 `Unpowered`（不吃[gold]力量[/gold]、不再触发对方的"受击反伤"链），与荆棘一致。
    /// </summary>
    public sealed class DotingMomPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        {
            if (target != base.Owner || dealer == null || !props.IsPoweredAttack())
            {
                return;
            }

            Flash();
            await CreatureCmd.Damage(choiceContext, dealer, base.Amount, ValueProp.Unpowered | ValueProp.SkipHurtAnim, base.Owner);
        }

        /// <summary>敌方回合结束时移除 = 效果覆盖敌方回合、到你的下个回合开始（照「听桥」）。</summary>
        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (base.Owner.Side != side)
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}