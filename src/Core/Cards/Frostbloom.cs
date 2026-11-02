using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>霜华迸发：1费罕见技能，给予5（7）层冰冻；目标已有冰冻时改为9（12）层。</summary>
    public sealed class Frostbloom : CardModel, ILingdawnpaoCard
    {
        protected override bool ShouldGlowGoldInternal =>
            CombatState?.HittableEnemies.Any(e => e.HasPower<FrostbitePower>()) ?? false;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(5m),
            new DynamicVar("FrostbloomBoost", 9m),
        ];

        public Frostbloom()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            decimal stacks = cardPlay.Target.HasPower<FrostbitePower>()
                ? DynamicVars["FrostbloomBoost"].BaseValue
                : DynamicVars["FrostbitePower"].BaseValue;
            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target,
                stacks, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(2m); // 5 → 7
            DynamicVars["FrostbloomBoost"].UpgradeValueBy(3m); // 9 → 12
        }
    }
}
