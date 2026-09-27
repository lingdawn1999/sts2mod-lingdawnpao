using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪场（玩家增益，由「雪场」卡赋予，Counter 可叠加）【v2 改造定稿】：
    /// **在你的回合开始时，将抽牌堆顶的 1 张牌埋入[gold]伏击区域[/gold]（不占每回合入区额度）**，
    /// 升级后 2 张。皮卡丘「浴电」同构的入流引擎——区域自动进料，玩家零操作。
    /// 用 AfterSideTurnStart 而不是 BeforeSideTurnStart（照本体 §SideTurnStart 注释里的口径：
    /// 涉及"获得资源"的用 After，避免和起手抽牌 / 回合开始结算抢顺序）。
    /// <para>
    /// ⚠️ 埋的是**抽牌堆顶的真牌**（<see cref="AmbushZone.BuryTopOfDrawPileAsync" />）：
    /// 抽牌堆空 / 区域已满时埋不进，本回合不进料（照「扑击」的既有约定，不做额外保护）。
    /// 不占额度——"主动埋卡超编"的正当渠道（规则 5）。
    /// </para>
    /// </summary>
    public sealed class SnowfieldPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner) || base.Amount <= 0m)
            {
                return;
            }

            Player? owner = base.Owner.Player;
            if (owner == null)
            {
                return;
            }

            // 逐张埋（层数 = 每回合埋入张数）；抽牌堆空 / 区域满时 BuryTopOfDrawPileAsync 返回 false
            for (int i = 0; i < (int)base.Amount; i++)
            {
                bool buried = await AmbushZone.BuryTopOfDrawPileAsync(new ThrowingPlayerChoiceContext(), owner);
                if (!buried)
                {
                    Log.Info($"雪场：第 {i + 1} 张进料失败（区域满或抽牌堆空），本回合停止进料");
                    break;
                }
            }
        }
    }
}
