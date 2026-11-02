using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>0费、消耗，升级保留。消耗任意张手牌，免费将等量的其他手牌置入伏击区。</summary>
public sealed class Concentration : CardModel, ILingdawnpaoCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public Concentration() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    private bool CanSelectHand(CardModel card) => card != this && card.Owner == Owner
        && card.Pile?.Type == PileType.Hand && AmbushStance.IsSelectable(card);

    private int AvailableSpaces => Math.Max(0, AmbushZone.For(Owner).Capacity - AmbushZone.For(Owner).Count);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!AmbushZone.CanContinueCombat(Owner)) return;
        // Exhaust any number first. Empty/full/disabled zones do not restrict
        // this payment; placement is capped afterwards by the live hand and slots.
        int limit = PileType.Hand.GetPile(Owner).Cards.Count(CanSelectHand);
        if (limit <= 0) return;

        var payments = (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(new LocString("cards", "CONCENTRATION.selectionScreenPrompt"), 0, limit), CanSelectHand, this))
            .Where(CanSelectHand).Distinct().Take(limit).ToList();
        if (payments.Count == 0) return;

        int paid = 0;
        foreach (CardModel payment in payments)
        {
            if (!AmbushZone.CanContinueCombat(Owner)) return;
            if (!CanSelectHand(payment)) continue;
            // Native Exhaust owns the hand node and exhaust hooks. Do not use
            // the silent post-auto-play removal path for cards still in hand.
            await CardCmdCompat.Exhaust(choiceContext, payment, causedByEthereal: false, skipVisuals: false);
            paid++;
        }
        if (!AmbushZone.CanContinueCombat(Owner) || AmbushZone.For(Owner).IsCoolingDown) return;
        // Hooks may draw cards, end combat or change capacity. Revalidate before
        // opening the second selector; never wait on a zero-option selection.
        int count = Math.Min(paid, Math.Min(AvailableSpaces, PileType.Hand.GetPile(Owner).Cards.Count(CanSelectHand)));
        if (count <= 0) return;
        var cards = (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(new LocString("cards", "CONCENTRATION.buryPrompt"), count)
            { PretendCardsCanBePlayed = true }, CanSelectHand, this)).Distinct().Take(count).ToList();
        foreach (CardModel card in cards)
        {
            if (!AmbushZone.CanContinueCombat(Owner) || AmbushZone.For(Owner).IsCoolingDown
                || AmbushZone.For(Owner).IsFull) break;
            if (CanSelectHand(card))
                await AmbushZone.BuryAsync(choiceContext, card, Owner, spendResources: false);
        }
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
