using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// 博闻的藏狐叔叔：**动物**衍生牌（Token），0 费技能，自带[gold]消耗[/gold]。
    /// 抽 **1** 张牌（升级 **2**）——纯过牌的小零件，和其他动物凑"每回合白来一张"的节奏。
    /// </summary>
    public sealed class TibetanFox : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/tibetan_fox.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public TibetanFox()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m);   // 1 → 2
        }
    }
}