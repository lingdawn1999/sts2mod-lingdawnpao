using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜息：**1 费**罕见（蓝）技能（2026-09-22 由 0 费改成 1 费）。给予 3 层冰冻，抽 1 张牌。
    /// 冰冻线的廉价挂冰源：自抽 1 张，等于净赚一张牌去垫层数，
    /// 专门喂「雪崩」（每层换伤害）、「冰封之刃」（有冰冻则翻倍）、「深冬」（已有冰冻则加量）这些吃层数的牌。
    /// **升级：抽牌 1 → 2**（原为"冰冻 3 → 4"）——用户 2026-09-22 改：升级位让给抽牌，
    /// 挂冰量固定 3（配合"层数减半"的新衰减口径，抬高挂冰量不如多抽一张更实在）。
    /// </summary>
    public sealed class FrostBreath : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(3m),
            new CardsVar(1),
        ];

        public FrostBreath()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target, DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 抽牌 1 → 2（冰冻固定 3）
        }
    }
}