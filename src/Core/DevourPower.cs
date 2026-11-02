using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core;

/// <summary>每层一个神速回合；层数包含当前正在执行的回合，在该回合结束时扣除。</summary>
public sealed class DevourPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public bool IsExtraTurnActive { get; private set; }

    public override bool ShouldTakeExtraTurn(Player player) => player == Owner.Player && Amount > 0 && !IsExtraTurnActive;

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (!ShouldTakeExtraTurn(player)) return;
        IsExtraTurnActive = true;
        await RefreshZoneAsync(Owner);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        // Do not spend a queued turn on the turn the card was played, an enemy turn or a teammate's turn.
        if (!IsExtraTurnActive || side != Owner.Side || !participants.Contains(Owner)) return;
        IsExtraTurnActive = false;
        await PowerCmd.Decrement(this);
        await RefreshZoneAsync(Owner);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        IsExtraTurnActive = false;
        await RefreshZoneAsync(oldOwner);
    }

    private static async Task RefreshZoneAsync(Creature creature)
    {
        if (creature.Player is not { } player || creature.IsDead
            || (!TestMode.IsOn && CombatManager.Instance.IsOverOrEnding)) return;
        await AmbushZone.RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), player);
        AmbushZoneUi.Refresh();
    }
}
