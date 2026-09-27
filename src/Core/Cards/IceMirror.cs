using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 冰镜：1 费金卡技能，**手牌复制**（2026-09-21 用户定稿）。
    /// 选择你[gold]手牌[/gold]中的 1 张牌，把 1 张**复制品**加入手牌，自带[gold]消耗[/gold]；
    /// **升级：复制 1 张 → 2 张**。
    /// **本体对照「双持」`DualWield`**（1 费 · 技能 · 自身：选 1 张**攻击/能力牌**，
    /// 将 1 张复制加入手牌，升级 2 张，见 sts2mod-unciphered/src/Core/Models/Cards/DualWield.cs；
    /// 本体把它做成**事件卡**且**无消耗**）。我们放开牌型限制（任意牌都能复制）+ 加[gold]消耗[/gold]，
    /// 用稀有度（金卡）与关键词给"复读关键牌"上价。
    /// **实现照本体**：`CardModel.CreateClone()`（[CardModel.cs](file:///d:/sts2mod/sts2mod-unciphered/src/Core/Models/CardModel.cs) 的注释明确写了
    /// "想在战斗里克隆牌（例如 DualWield 这种效果）就用 CreateClone"）
    /// → `CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Hand, Owner)`。
    /// ⚠️ `CreateClone` 要求源牌在**战斗牌堆**里（手牌满足），别对它做牌堆外的克隆。
    /// </summary>
    public sealed class IceMirror : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public IceMirror()
            : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            CardModel? picked = (await CardSelectCmd.FromHand(
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                context: choiceContext,
                player: Owner,
                filter: null,
                source: this)).FirstOrDefault();

            if (picked == null)
            {
                return;
            }

            for (int i = 0; i < DynamicVars.Cards.IntValue; i++)
            {
                CardModel clone = picked.CreateClone();
                await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Hand, Owner);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m);   // 1 张 → 2 张
        }
    }
}