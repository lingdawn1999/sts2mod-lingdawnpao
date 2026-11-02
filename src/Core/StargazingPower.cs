using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 观星（玩家增益，由「观星」卡赋予，无层数语义 → `Single`）：
    /// **在你的回合开始时（摸牌之后），查看抽牌堆顶的 5 张牌，可以将其中的任意张放进弃牌堆**
    /// （未选中的牌保持原顺序留在牌堆顶）。
    /// <para>
    /// **⚠️ 2026-10-28 修 bug：交互从 `BeforeSideTurnStart` 挪到 `AfterPlayerTurnStart`**。
    /// 原因：`BeforeSideTurnStart` 的暂停-恢复机制**不等玩家交互**——UI 一弹，回合流程立刻继续，
    /// 摸牌把正在查看的顶牌直接抽进手（实测报障："出现 UI 了但还没交互就把牌塞手上了"）。
    /// 本体该钩子的 6 个实现**全是自动效果，没有一个弹玩家交互**——这不是巧合，是雷区。
    /// `AfterPlayerTurnStart`（摸牌后、获得操作权前）才是交互安全区（本体 `ToolsOfTheTrade`
    /// "回合开始抽1弃1" 的交互先例位置）；挪过来后玩家看的是"接下来要摸的 5 张"，规划下回合摸牌，
    /// 更贴"预见"的本意。
    /// **联机**：`player != Owner.Player` 过滤，只在自己一侧触发；交互走 PlayerChoiceSynchronizer 同步。
    /// **顶牌顺序**：`PileType.Draw.GetPile(owner).Cards` 索引 0 = 牌堆顶（本体 `CardPileCmd.Draw` 取的就是 `Cards.FirstOrDefault()`）。
    /// **选择范围**：`CardSelectCmd.FromCombatPile(..., filter: top.Contains)` 把可选牌限死在这 5 张内，
    /// `min = 0` 允许一张都不弃；提示用本体内置的 `CardSelectorPrefs.DiscardSelectionPrompt`（无需自写 selectionScreenPrompt 键）。
    /// </para>
    /// </summary>
    public sealed class StargazingPower : PowerModel
    {
        /// <summary>每次查看的牌数。</summary>
        private const int LookCount = 5;

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        {
            if (player != base.Owner.Player)
            {
                return;
            }

            CardPile drawPile = PileType.Draw.GetPile(player);
            List<CardModel> top = drawPile.Cards.Take(LookCount).ToList();
            if (top.Count == 0)
            {
                return;
            }

            IEnumerable<CardModel> picked = await CardSelectCmd.FromCombatPile(
                context: choiceContext,
                pile: drawPile,
                player: player,
                prefs: new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, top.Count),
                filter: top.Contains);

            await CardCmd.Discard(choiceContext, picked);
        }
    }
}