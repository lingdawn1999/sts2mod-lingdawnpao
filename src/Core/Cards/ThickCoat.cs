using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 厚绒：1费普通技能，获得4（6）点格挡。从手牌打出后加入伏击区域。
    /// 带出等自动打出不再入区；重放只在最后一次结算后入区，保留消耗等特殊去向。
    /// </summary>
    public sealed class ThickCoat : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            // 2026-10-28 维持 4：曾提过 5，但打出 5 甲 + 入区还会触发积雪联动，
            // 升级 7 的账面总防越过白卡线——这张的价值在"循环入区"，不在格挡数值。
            new BlockVar(4m, ValueProp.Move),
        ];

        public ThickCoat()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            await ResolvePlacementAsync(choiceContext, cardPlay);
        }

        internal async Task ResolvePlacementAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (cardPlay.IsAutoPlay || !AmbushZone.CanContinueCombat(Owner)) return;

            // Preserve the relic's ordinary old-card bring-out, before adding this card.
            // Automatic plays skip both steps so bringing out Thick Coat cannot recurse.
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            if (cardPlay.IsLastInSeries && cardPlay.ResultPile == PileType.Discard)
                await AmbushZone.BuryPlayedCardAsync(choiceContext, Owner, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(2m);   // 4 → 6
        }
    }
}
