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
    /// 过载（玩家增益，由「过载」卡赋予）：管理伏击区的"过载 → 冷却 → 恢复"两回合状态机。
    /// 打出过载卡的当回合：IsOverloaded（区内牌视为所有类型）。
    /// 下个玩家回合开始：转为 IsCoolingDown（整个区域失效一回合）。
    /// 再下个回合开始：冷却结束、恢复常态，并移除本能力。
    /// </summary>
    public sealed class OverloadPower : PowerModel
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
            if (zone.IsOverloaded)
            {
                zone.IsOverloaded = false;
                zone.IsCoolingDown = true;
                Log.Info("伏击区域：过载结束，进入冷却（本回合区域失效）");
            }
            else if (zone.IsCoolingDown)
            {
                zone.IsCoolingDown = false;
                Log.Info("伏击区域：冷却结束，恢复正常");
                await AmbushZone.RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), base.Owner.Player);
                AmbushZoneUi.Refresh();
                await PowerCmd.Remove(this);
                return;
            }
            else
            {
                // 状态异常（例如战斗重置）：直接移除，避免残留
                await PowerCmd.Remove(this);
                return;
            }

            await AmbushZone.RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), base.Owner.Player);
            AmbushZoneUi.Refresh();
        }
    }
}