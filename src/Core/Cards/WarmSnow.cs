using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 暖雪：0 费普通技能，抽 2 张牌（升级 3），自带消耗。
    /// 纯过滤件：0 费过两张再消耗，等于净赚一张牌，用来找伏击流的组合件或冰冻的挂冰牌。
    /// </summary>
    public sealed class WarmSnow : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(2),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

        public WarmSnow()
            : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 2 → 3
        }
    }
}