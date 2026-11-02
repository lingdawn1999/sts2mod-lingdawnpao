using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>旧凝神/背水一战的本回合失效在下个玩家回合解除，与过载的下回合失效独立。</summary>
    public sealed class ConcentrationPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner) || base.Owner.Player == null)
            {
                return;
            }

            AmbushZoneState zone = AmbushZone.For(base.Owner.Player);
            zone.IsCoolingDown = false;
            Log.Info("伏击区域：本回合失效结束，恢复正常（凝神 / 背水一战）");
            await AmbushZone.RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), base.Owner.Player);
            AmbushZoneUi.Refresh();
            await PowerCmd.Remove(this);
        }
    }
}
