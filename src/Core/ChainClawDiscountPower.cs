using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>本回合的免费攻击次数。自动带出不消耗，手动打出和付费入区各消耗一次。</summary>
public sealed class ChainClawDiscountPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Resolve before Frost Throne's late loan decision, as the old discount did.
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!AppliesTo(card)) return false;
        modifiedCost = 0m;
        return true;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // Brought-out cards are already free; automatic plays preserve the remaining manual uses.
        if (!cardPlay.IsAutoPlay) await ConsumeForManualUseAsync(cardPlay.Card);
    }

    // Burial bypasses BeforeCardPlayed; consume exactly one use after its free payment.
    internal async Task ConsumeForManualUseAsync(CardModel card)
    {
        if (AppliesTo(card)) await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Player is { } player) AmbushStance.RefreshHandVisuals(player);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player is { } player) AmbushStance.RefreshHandVisuals(player);
        return Task.CompletedTask;
    }

    private bool AppliesTo(CardModel card) => Amount > 0 && card.Owner.Creature == Owner
        && card.Type == CardType.Attack && !card.EnergyCost.CostsX
        && card.Pile?.Type is PileType.Hand or PileType.Play;
}
