using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>记录过载的下回合代价，不改变当前回合的区域属性。</summary>
public sealed class OverloadPower : PowerModel
{
    private int _activatesAtTurn = int.MaxValue;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Owner.Player is not { } player
            || player.PlayerCombatState is not { } combat || combat.TurnNumber < _activatesAtTurn) return;
        var exhaustion = Owner.GetPower<AmbushExhaustionPower>()
            ?? await PowerCmd.Apply<AmbushExhaustionPower>(choiceContext, Owner, 1m, Owner, null);
        exhaustion?.RefreshDuration();
        await AmbushZone.RecomputeBonusesAsync(choiceContext, player);
        AmbushZoneUi.Refresh();
        await PowerCmd.Remove(this);
    }

    internal void ScheduleForNextTurn()
    {
        if (Owner?.Player?.PlayerCombatState is { } combat) _activatesAtTurn = combat.TurnNumber + 1;
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ScheduleForNextTurn();
        return Task.CompletedTask;
    }
}
