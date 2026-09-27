using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 吵闹的乌鸦：**动物**衍生牌（Token，**垃圾档**），0 费技能，自带[gold]消耗[/gold]。
    /// 抽 **1** 张牌，然后弃 **1** 张牌（升级：抽 2 弃 2）。
    /// **垃圾定位**：净换牌数 = 0，只是把手上不要的换掉，不像「博闻的藏狐叔叔」是白赚牌。
    /// 写法照本体「杂技」`Acrobatics`（1 费蓝卡：抽 3 弃 1，见
    /// sts2mod-unciphered/src/Core/Models/Cards/Acrobatics.cs）：`CardPileCmd.Draw` →
    /// `CardSelectCmd.FromHandForDiscard(..., new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, n), null, this)`
    /// → `CardCmd.Discard`。**弃牌提示用本体内置的 `DiscardSelectionPrompt`**，不需要自己写 selectionScreenPrompt 键。
    /// </summary>
    public sealed class NoisyRaven : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/noisy_raven.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public NoisyRaven()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int count = DynamicVars.Cards.IntValue;
            await CardPileCmd.Draw(choiceContext, count, Owner);

            IEnumerable<CardModel> picked = await CardSelectCmd.FromHandForDiscard(
                choiceContext, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, count),
                null, this);

            await CardCmd.Discard(choiceContext, picked);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m);   // 抽 1 弃 1 → 抽 2 弃 2
        }
    }
}