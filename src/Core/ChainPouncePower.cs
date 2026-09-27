using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 连扑（玩家增益，由「连扑」金卡赋予）：本回合内，**从伏击区域带出的**下 N 张牌会额外打出一遍。
    /// 层数就是"还能翻倍几张"的上限（打完一张扣一层），所以它同时是这张卡的阀门：
    /// 不管区域堆到几格、联动多顺，本回合额外的结算次数都被层数锁死。
    /// 结构照本体 OneTwoPunchPower（同样是"下 N 张牌额外打出一次 + 用完扣层 + 回合结束移除"），
    /// 只多一个 `AmbushZone.IsAmbushPlay` 判定——只认带出的牌，直接打在手里的牌不吃。
    /// "结算后消耗"由 AmbushZone.BringOutFirstAsync 处理（打出前读层数、结算后送进消耗堆）。
    /// </summary>
    public sealed class ChainPouncePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
        {
            if (card.Owner.Creature != base.Owner || !AmbushZone.IsAmbushPlay(card))
            {
                return playCount;
            }

            return playCount + 1;
        }

        public override async Task AfterModifyingCardPlayCount(CardModel card)
        {
            await PowerCmd.Decrement(this);
        }

        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (participants.Contains(base.Owner))
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}