using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 可怕的兔狲叔叔：**动物**衍生牌（Token），0 费技能，自带[gold]消耗[/gold]。
    /// 给予目标 **1** 层[gold]虚弱[/gold]（升级 **2**）。单体高中断件，对标「哈气」的定位但去掉抽牌、换 0 费。
    /// </summary>
    public sealed class PallasCat : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/pallas_cat.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
            HoverTipFactory.FromPower<WeakPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<WeakPower>(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public PallasCat()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target,
                DynamicVars.Weak.BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Weak.UpgradeValueBy(1m);   // 1 → 2
        }
    }
}