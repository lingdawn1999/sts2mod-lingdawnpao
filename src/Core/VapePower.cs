using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 电子烟（玩家增益，由「电子烟」卡赋予，Counter 可叠加）：
    /// **在你的回合开始时，抽等同层数的牌**（层数 = 打出的「电子烟」张数，多张叠加就多抽）。
    /// <para>
    /// 钩子选 `AfterPlayerTurnStart`：本体回合开始的顺序是
    /// 「清格挡 → BeforeSideTurnStart → 能量重置 → 摸牌 → AfterPlayerTurnStart → AfterSideTurnStart」，
    /// 这个钩子既在**起手抽牌之后**（不会和本体的抽牌抢顺序），又带 `PlayerChoiceContext`
    /// ——本体的抽牌命令 `CardPileCmd.Draw` 需要它，而 `AfterSideTurnStart` 那条签名没有 choiceContext，
    /// 所以抽牌类效果放在这里（照「暗杀者」在同一个钩子里做手牌选择）。
    /// </para>
    /// </summary>
    public sealed class VapePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        {
            if (player != base.Owner.Player || base.Amount <= 0m)
            {
                return;
            }

            await CardPileCmd.Draw(choiceContext, base.Amount, player);
        }
    }
}