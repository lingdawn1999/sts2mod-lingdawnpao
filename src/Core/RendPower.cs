using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 撕裂：敌方 Debuff（本回合）。受到攻击伤害时，额外受到层数点伤害；每段攻击伤害各触发一次。
    /// 回合结束自动清空，防止层数无限成长。静态防重入：撕裂附加的伤害不再二次触发。
    /// </summary>
    public sealed class RendPower : PowerModel
    {
        private static bool _resolving;

        public override PowerType Type => PowerType.Debuff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
        {
            // 仅攻击伤害触发（ValueProp.Move = 攻击牌/怪物攻击）；毒/遗物等 Unpowered、Unblockable 伤害不算。
            if (_resolving || target != base.Owner || !props.HasFlag(ValueProp.Move) || result.UnblockedDamage <= 0)
            {
                return;
            }

            _resolving = true;
            try
            {
                Flash();
                // dealer 是可空的（伤害可能非生物来源），所以走兼容层那组 `Creature? dealer + cardSource`
                // 重载；5 参重载的 dealer 声明为不可空，传 null 会报 CS8604（运行期行为其实一样，但别留警告）。
                await CreatureCmdCompat.Damage(choiceContext, base.Owner, base.Amount,
                    ValueProp.Unpowered | ValueProp.SkipHurtAnim, dealer, null, null);
            }
            finally
            {
                _resolving = false;
            }
        }

        // 本回合效果：挂上它的那一方回合结束时清空（施加只发生在玩家回合）
        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (side == base.Owner.Side && participants.Contains(base.Owner))
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}
