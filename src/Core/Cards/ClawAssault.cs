using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪爪突击：1 费普通攻击，"伤害 + 过牌"的运转型攻击（2026-09-21 用户定稿）。
    /// 造成 9 点伤害，抽 1 张牌；升级 10 伤 + 抽 2。数值与升级**完整照抄本体「剑柄打击」PommelStrike**
    /// （1 费白攻击 9 伤抽 1，升级 10 伤抽 2，见 sts2mod-unciphered/src/Core/Models/Cards/PommelStrike.cs）。
    /// **为什么这张必须做**：本 mod 接下来要补的 17 张**全是攻击牌**，技能位不再增加，运转会吃紧；
    /// 而这形状在池内是空缺——现有攻击牌只有 6 种形状（纯伤/伤害+格挡/伤害+挂冰/高伤+自伤/全体多段/
    /// 区域减费），没有一张"打伤害的同时过牌"。它一张顶两张：同时补攻击浓度与过牌。
    /// 结算顺序：**先伤害、再抽牌**（照本体），抽到的牌当回合可用。
    /// </summary>
    public sealed class ClawAssault : CardModel, ILingdawnpaoCard
    {
        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(9m, ValueProp.Move),
            new CardsVar(1),
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

            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(1m); // 9 → 10
            DynamicVars.Cards.UpgradeValueBy(1m);  // 抽 1 → 抽 2
        }
    }
}