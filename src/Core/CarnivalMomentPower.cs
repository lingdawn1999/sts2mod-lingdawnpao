using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻（玩家增益，由「狂欢时刻」卡赋予，Counter 可叠加）：
    /// **2026-09-27 用户三改（溅射"永久全局修正太猎奇"→ 回合内滚动加成，卡费 0 → 3）**：
    /// 当前回合你每造成 1 次冰冻，本回合**下一次**冰冻的层数 +1（首次 +0，逐次递增）。
    /// Amount 从 1 起（1 = 无加成的标记态），每次顶层挂冰读 Amount-1 加算后 +1；
    /// **回合开始时重置回 1**（见 <see cref="AfterSideTurnStart" />）。
    /// 结算收口在 <see cref="FrostbitePower.ApplyFrostAsync" />（顶层施加计一次，
    /// 批量挂冰整批算 1 次）；与「凝寒」的全程成长计数相互独立、同时生效。
    /// </summary>
    public sealed class CarnivalMomentPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        // 回合开始把滚动计数清零（Amount 回到标记态 1）：上一回合攒的连击次数不跨回合
        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner) || base.Amount <= 1)
            {
                return;
            }

            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, 1m - base.Amount, base.Owner, null);
        }
    }
}