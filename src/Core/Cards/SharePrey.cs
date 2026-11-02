using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 分享猎物：联机专属，1费罕见技能，消耗。
    /// 所有存活玩家（包括自己）获得1（2）张猎物+。
    /// </summary>
    public sealed class SharePrey : CardModel, ILingdawnpaoCard
    {
        public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public SharePrey()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int count = DynamicVars.Cards.IntValue;

            ICombatState? combatState = CombatState;
            if (combatState == null) return;

            IEnumerable<Creature> allies = from c in combatState.GetTeammatesOf(Owner.Creature)
                                           where c != null && c.IsAlive && c.IsPlayer
                                           select c;

            foreach (Creature ally in allies)
            {
                Player? allyOwner = ally.Player;
                if (allyOwner == null) continue;

                var generated = new List<CardModel>(count);
                for (int i = 0; i < count; i++)
                {
                    CardModel prey = ally.CombatState!.CreateCard(ModelDb.Card<Prey>(), allyOwner);
                    CardCmd.Upgrade(prey); // 猎物+
                    generated.Add(prey);
                }
                if (generated.Count > 0)
                {
                    await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, allyOwner);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 1 → 2
        }
    }
}
