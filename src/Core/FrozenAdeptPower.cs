using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 冰封（玩家增益，由「冰封」技能牌赋予，Counter 可叠加）：
    /// **本回合**每层使挂冰的伤害结算额外触发一次（同回合抓两张技能牌可叠到 +2、+3…）。
    /// 临时能力：玩家回合结束时移除（同本体 OneTwoPunchPower / 我们的 FlipFlopPower 的处理），
    /// 这正是「冰封」从能力牌降为技能牌的落点——效果只覆盖打出的那一回合。
    /// </summary>
    public sealed class FrozenAdeptPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>挂冰伤害的额外触发次数（= 能力层数）。</summary>
        public int ExtraTriggers => (int)Amount;

        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (participants.Contains(base.Owner))
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}