using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 探爪：1 费普通攻击（2026-09-27 用户重做，去本体化）。
    /// 造成 4 点伤害，获得 4 点[gold]格挡[/gold]，**抽 1 张牌**。升级：**6 伤 / 6 挡**（每项 +2，抽牌不变）。
    /// <para>
    /// **沿革**：初版整张照抄本体「铁斩波」IronWave（1 费 5/5 → 升级 7/7，数值逐点相同，零扭曲）。
    /// 2026-09-27 用户定"不再扩池、改造照抄卡"方向后，本卡作为**白卡位头号照抄重灾区**率先重做：
    /// 数值降为 4/4，挂上**抽 1**——变成 mod 首张"伤/挡/抽"三合一日攻击（强度对齐本体剑柄打击的
    /// "9 伤抽 1"，攻防分流不算超模），同时补上白卡的过牌供应。
    /// </para>
    /// </summary>
    public sealed class ProbingClaw : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(4m, ValueProp.Move),
            new BlockVar(4m, ValueProp.Move),
        ];

        public ProbingClaw()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);   // 4 → 6
            DynamicVars.Block.UpgradeValueBy(2m);    // 4 → 6（抽牌不升）
        }
    }
}