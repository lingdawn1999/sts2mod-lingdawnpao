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

/// <summary>区域衰竭使伏击区域失效，按本人回合序号到期；连续过载刷新期限，不依赖钩子顺序。</summary>
public sealed class AmbushExhaustionPower : PowerModel
{
    private int _expiresAtTurn = int.MaxValue;
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner) && Owner.Player?.PlayerCombatState is { } combat
            && combat.TurnNumber >= _expiresAtTurn) await PowerCmd.Remove(this);
    }

    internal void RefreshDuration()
    {
        if (Owner?.Player?.PlayerCombatState is { } combat) _expiresAtTurn = combat.TurnNumber + 1;
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDuration();
        if (Owner.Player is { } player)
        {
            await AmbushZone.RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), player);
            AmbushZoneUi.Refresh();
        }
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player is { } player && oldOwner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await AmbushZone.RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), player);
            AmbushZoneUi.Refresh();
        }
    }
}
