using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 破冰猛击：2 费罕见（蓝）攻击，**有条件加伤**（2026-09-21 用户定稿）。
    /// 造成 12 点伤害；若目标有[gold]冰冻[/gold]，**额外造成 8 点**（有条件时共 20）。
    /// 升级 12 → 16 伤、额外 8 → 10（有条件时共 26）。
    /// **为什么必须做**：本 mod 的冰冻线里，按层数兑现的只有**金卡**「雪崩」，蓝卡档缺一张
    /// "挂着冰就打得动"的条件攻击件——它把"先挂冰"这件事从处决进度变成**即时回报**。
    /// 本体对照：`Cruelty`（1 费金能力：对有易伤的敌人额外 25% 伤害）；
    /// **刻意不照抄"翻倍"**：mod 挂冰速度很快、条件几乎恒满足，翻倍就等于无条件高伤（会越界）。
    /// 有条件时总伤 20，对标本体 2 费蓝卡的 20 价值线（`Uppercut` 2 费 13 伤 + 双 debuff、
    /// `Dash` 2 费 10 伤 10 挡）——多出的条件是溢价，不是白给。
    /// **判定与结算**：先查冰冻、再把两段合并成**一次伤害**打出（不给格挡两次减免的机会，
    /// 也让"处决判定"用完整数值走一遍）。
    /// </summary>
    public sealed class Icebreaker : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(12m, ValueProp.Move),
            new DamageVar("BonusDamage", 8m, ValueProp.Move),
        ];

        public Icebreaker()
            : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            decimal total = DynamicVars.Damage.BaseValue;
            if (cardPlay.Target.GetPower<FrostbitePower>() != null)
            {
                total += DynamicVars["BonusDamage"].BaseValue;
            }

            await DamageCmd.Attack(total).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(4m);          // 12 → 16
            DynamicVars["BonusDamage"].UpgradeValueBy(2m);  // 额外 8 → 10
        }
    }
}