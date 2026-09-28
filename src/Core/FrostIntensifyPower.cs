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
    /// 凝寒（玩家增益，由「凝寒」金卡赋予，2026-09-28 用户定稿改为**回合内滚动**）：
    /// 当前回合你每造成 1 次冰冻，本回合**下一次**冰冻的层数 +1（首次 +0，逐次递增）。
    /// Amount 从 1 起（1 = 无加成的标记态），每次顶层挂冰读 Amount-1 加算后 +1；
    /// **回合开始时重置回 1**（见 <see cref="AfterSideTurnStart" />）。
    /// 结算收口在 <see cref="FrostbitePower.ApplyFrostAsync" />；**对群分别生效**（2026-09-28 用户追加定稿）：
    /// 批量挂冰里每个敌人各吃一次当前加成、各推进一次计数（3 敌批 = +0/+1/+2 逐敌爬升）；
    /// 与「狂欢时刻」的雪球静态加成相互独立、同时生效。
    /// 注意：只对 `applier != null` 的挂冰生效——「冰封王座」把冰冻挂到玩家自己身上时 applier 为空。
    /// </summary>
    public sealed class FrostIntensifyPower : PowerModel
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
