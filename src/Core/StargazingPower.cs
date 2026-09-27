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
    /// **在你的回合开始时，查看抽牌堆顶的 5 张牌，可以将其中的任意张放进弃牌堆**
    /// （未选中的牌保持原顺序留在牌堆顶）。
    /// **⚠️ 钩子选 `BeforeSideTurnStart` 而不是 `AfterSideTurnStart`**：本能力**需要玩家做选择**，
    /// 而只有 `BeforeSideTurnStart` 的签名带 `PlayerChoiceContext choiceContext`
    /// （本体 `AggressionPower` 就是在这个钩子里操作玩家的牌堆，见
    /// sts2mod-unciphered/src/Core/Models/Powers/AggressionPower.cs）；mod 内「雪场」「动物朋友」用 After 是因为它们**只生成资源、不需要玩家交互**。
    /// **联机**：`participants.Contains(Owner)` 过滤，只在自己回合触发。
    /// **顶牌顺序**：`PileType.Draw.GetPile(owner).Cards` 索引 0 = 牌堆顶（本体 `CardPileCmd.Draw` 取的就是 `Cards.FirstOrDefault()`）。
    /// **选择范围**：`CardSelectCmd.FromCombatPile(..., filter: top.Contains)` 把可选牌限死在这 5 张内，
    /// `min = 0` 允许一张都不弃；提示用本体内置的 `CardSelectorPrefs.DiscardSelectionPrompt`（无需自写 selectionScreenPrompt 键）。
    /// </summary>
    public sealed class StargazingPower : PowerModel
    {
        /// <summary>每次查看的牌数。</summary>
        private const int LookCount = 5;

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner))
            {
                return;
            }

            Player? owner = base.Owner.Player;
            if (owner == null)
            {
                return;
            }

            CardPile drawPile = PileType.Draw.GetPile(owner);
            List<CardModel> top = drawPile.Cards.Take(LookCount).ToList();
            if (top.Count == 0)
            {
                return;
            }

            IEnumerable<CardModel> picked = await CardSelectCmd.FromCombatPile(
                context: choiceContext,
                pile: drawPile,
                player: owner,
                prefs: new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 0, top.Count),
                filter: top.Contains);

            await CardCmd.Discard(choiceContext, picked);
        }
    }
}