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
    /// <summary>
    /// 本回合冷却的记账能力（由「凝神」与「背水一战」共用）：卡牌把伏击区置为"冷却"后，
    /// **在下个玩家回合开始时解除冷却、恢复常态，并自解**——只做这一件事的记账能力。
    /// **为什么需要它**：`AmbushZoneState.IsCoolingDown` 原本只由 `OverloadPower` 归零
    /// （它管的是"过载 → 冷却 → 恢复"两回合状态机）。凡是"当回合直接冷却"的卡（凝神、背水一战），
    /// 若不配这个能力，伏击区会**永久卡在冷却**。结构照 `OverloadPower` 的恢复分支写，两者互不干扰。
    /// **新增同类卡时直接复用本能力**（`StackType.Single`，重复施加无副作用），不要各写一个。
    /// </summary>
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

            // 「过载」在场时，冷却的生命周期归它的状态机管（它还要走"冷却 → 恢复"两回合），
            // 本能力只自解、不碰 IsCoolingDown，否则会把过载的"下回合失效"提前解掉。
            if (base.Owner.GetPower<OverloadPower>() != null)
            {
                await PowerCmd.Remove(this);
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