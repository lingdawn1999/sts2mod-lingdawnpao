using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>1费、不消耗，升级降为0费。自选带出一张牌并免除其埋伏等待，再免费放入一张手牌。</summary>
public sealed class Regroup : CardModel, ILingdawnpaoCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description"))];
    public Regroup() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!AmbushZone.CanContinueCombat(Owner) || AmbushZone.For(Owner).IsCoolingDown) return;
        CardModel? buried = await AmbushZone.SelectOneAsync(choiceContext, Owner,
            new LocString("cards", "REGROUP.selectionScreenPrompt"));
        if (buried != null)
            await AmbushZone.BringOutChosenAsync(choiceContext, null, Owner, buried, ignoreAmbushDelay: true);
        if (!AmbushZone.CanContinueCombat(Owner) || AmbushZone.For(Owner).IsFull
            || AmbushZone.For(Owner).IsCoolingDown) return;
        CardModel? handCard = (await CardSelectCmd.FromHand(
            context: choiceContext, player: Owner,
            prefs: new CardSelectorPrefs(new LocString("cards", "REGROUP.buryPrompt"), 1)
            { PretendCardsCanBePlayed = true },
            filter: card => AmbushStance.IsSelectable(card) && card != this, source: this)).FirstOrDefault();
        if (handCard?.Pile?.Type == PileType.Hand && handCard.Owner == Owner)
            await AmbushZone.BuryAsync(choiceContext, handCard, Owner, spendResources: false);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
