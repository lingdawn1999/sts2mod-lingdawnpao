using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>雪拥：2（1）费，保留、消耗。敌人合计每2层冰冻获得1覆甲，向下取整，不消耗冰冻。</summary>
    public sealed class Snowdrift : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<PlatingPower>(),
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        /// <summary>每2层冰冻兑换1覆甲。</summary>
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<PlatingPower>(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

        public Snowdrift()
            : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            IReadOnlyList<Creature>? enemies = Owner.Creature.CombatState?.HittableEnemies;
            if (enemies == null)
            {
                return;
            }

            decimal frost = 0m;
            foreach (Creature enemy in enemies)
            {
                frost += enemy.GetPower<FrostbitePower>()?.Amount ?? 0m;
            }

            decimal plating = ConvertFrostToPlating(frost);
            if (plating <= 0m)
            {
                return;
            }

            await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature, plating, Owner.Creature, this);
        }

        internal static decimal ConvertFrostToPlating(decimal totalFrost) => decimal.Floor(totalFrost / 2m);

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 2 → 1，转化比例不变
        }
    }
}