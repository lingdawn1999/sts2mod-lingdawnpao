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
    /// 猛狐连击：**衍生牌**（Token），1 费攻击，自带[gold]消耗[/gold]。
    /// 你[gold]伏击区域[/gold]中**每有一张牌，就造成 5 点伤害 1 次**——段数 = 区域张数。
    /// **升级：1 费 → 0 费**（用户 2026-09-21 定案，取代原先的"每段 5→8"加伤）。
    /// 理由（用户原话）："能随机到这个本来也算他运气好，更何况有消耗，给大方一点"。
    /// 由「好学的狐狸哥哥」生成（它是金卡「动物朋友」随机发下来的动物之一）。
    /// **与「倾巢而出」的区别（别搞混）**：倾巢而出是**单段**伤害 = 基础 + 每张×加值；
    /// 本卡是**多段**——每段各吃一次[gold]力量[/gold]、也各被[gold]格挡[/gold]抵一次，
    /// 所以区域满格时它比倾巢而出更吃力量、也更怕格挡。
    /// 段数取 `AmbushZone.For(Owner).Count`，区域为空则不打（`hits <= 0` 直接返回）。
    /// 卡面文案不显示合计（多段伤害显示总量会误导：格挡/力量逐段结算），
    /// 只写"每有一张牌就造成一次 5 点伤害"（2026-09-23 用户改：原句式"造成5点伤害1次"不通顺）。
    /// </summary>
    public sealed class FierceFoxCombo : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/fierce_fox_combo.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(5m, ValueProp.Move),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public FierceFoxCombo()
            : base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            int hits = AmbushZone.For(Owner).Count;
            if (hits <= 0)
            {
                return;
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1);   // 1 费 → 0 费（数值不变，加伤已取消）
        }
    }
}