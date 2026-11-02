using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>1（0）费罕见技能，消耗。按区域张数获得能量并抽牌，下回合伏击区域失效。</summary>
    public sealed class Overload : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<OverloadPower>(),
            HoverTipFactory.FromPower<AmbushExhaustionPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public Overload()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (!AmbushZone.CanContinueCombat(Owner)) return;
            // 抽牌或出牌后的带出可能改变区域，资源统一按本次结算开始时的张数发放。
            int count = AmbushZone.For(Owner).Count;
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            var pending = await PowerCmd.Apply<OverloadPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            pending?.ScheduleForNextTurn();
            if (count <= 0) return;
            await PlayerCmd.GainEnergy(count, Owner);
            if (AmbushZone.CanContinueCombat(Owner)) await CardPileCmd.Draw(choiceContext, count, Owner);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1);
        }
    }
}
