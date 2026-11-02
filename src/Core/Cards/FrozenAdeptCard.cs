using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜冻回响：1费罕见能力。冰冻伤害额外触发1次，升级为2次，持续整场战斗。
    /// 升级不再降费；每段冰冻仍独立结算伤害、层数减半与处决。
    /// FrozenAdeptPower可叠加，纯叠层和融冬仍不触发伤害或减半。
    /// </summary>
    public sealed class FrozenAdeptCard : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrozenAdeptPower>(),
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrozenAdeptPower>(1m),
        ];

        public FrozenAdeptCard()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<FrozenAdeptPower>(choiceContext, Owner.Creature,
                DynamicVars["FrozenAdeptPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrozenAdeptPower"].UpgradeValueBy(1m); // 额外触发1次 → 2次，仍为1费
        }
    }
}
