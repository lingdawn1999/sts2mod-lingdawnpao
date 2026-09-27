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
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪爪突击：1 费普通攻击，"伤害 + 过牌 + 伏击回报"的运转型攻击。
    /// 造成 9 点伤害，抽 1 张牌；**[gold]埋伏[/gold]：额外抽 1 张牌**（埋伏打出时抽 2）。
    /// 升级：伤 +1（9→10）、抽 +1（1→2，埋伏抽 2→3）。
    /// <para>
    /// **沿革**：初版数值与升级**完整照抄本体「剑柄打击」PommelStrike**（1 费白攻 9 伤抽 1，升级 10 伤抽 2）。
    /// 2026-09-27 用户定"不再扩池、改造照抄卡"方向后，本卡挂上**埋伏段**去本体化：
    /// 从纯剑柄换皮变成伏击线回报件（埋进去跨回合带出 = 抽 2，进金框家族
    /// <see cref="IAmbushBonusCard" />）。
    /// 结算顺序：**先伤害、再抽牌**（照本体），抽到的牌当回合可用。
    /// </para>
    /// </summary>
    public sealed class ClawAssault : CardModel, ILingdawnpaoCard, IAmbushBonusCard
    {
        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(9m, ValueProp.Move),
            new CardsVar(1),
            new CardsVar("AmbushCards", 1),
        ];

        public ClawAssault()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            decimal cards = DynamicVars.Cards.BaseValue;
            if (AmbushZone.IsVeteranAmbush(this))
            {
                cards += DynamicVars["AmbushCards"].BaseValue;
            }

            await CardPileCmd.Draw(choiceContext, cards, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(1m); // 9 → 10
            DynamicVars.Cards.UpgradeValueBy(1m);  // 抽 1 → 抽 2（埋伏抽 2 → 3）
        }
    }
}