using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 风雪的庇护：**动物**衍生牌（Token），0 费技能，自带[gold]消耗[/gold]。
    /// 获得 **6** 点[gold]格挡[/gold]（升级 **9**）——动物里的纯防守件。
    /// </summary>
    public sealed class SnowShelter : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/snow_shelter.tres";
        public override bool GainsBlock => true;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(6m, ValueProp.Move),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public SnowShelter()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(3m);   // 6 → 9
        }
    }
}