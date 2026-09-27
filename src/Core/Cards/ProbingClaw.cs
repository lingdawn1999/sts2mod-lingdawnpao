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
    /// 探爪：1 费普通攻击（通用牌，2026-09-21 用户定稿）。
    /// 造成 5 点伤害，获得 5 点格挡。升级：**7 伤 / 7 挡**（每项 +2）。
    /// 定位：攻防一体的通用件——数值与升级数值**直接照抄本体「铁斩波」IronWave**
    /// （1 费 5/5 → 升级 7/7，见 sts2mod-unciphered/src/Core/Models/Cards/IronWave.cs），任意卡组能塞；
    /// 让"防御浓度上来"的同时不至于把输出压没，属于既补防线又保节奏的一档。
    /// **吞噬合成轴**：伤害段由引擎通用读取；格挡段作为非伤害特效接线（各执行一次不乘段）。
    /// </summary>
    public sealed class ProbingClaw : CardModel, ILingdawnpaoCard, IFusionMaterial
    {
        public override bool GainsBlock => true;

        /// <summary>合成材料轴：格挡段（伤害段走 DynamicVars 通用读取，无需声明）。</summary>
        public async Task PlayFusionEffectAsync(PlayerChoiceContext choiceContext, CardPlay fusionPlay) =>
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, fusionPlay);

        /// <summary>合成卡面附加段：格挡数值合成时刻烘焙（沿用本体格挡口径，不吃后续加成）。</summary>
        public string FusionEffectLine
        {
            get
            {
                LocString line = new("cards", "PROBING_CLAW.fusionEffect");
                line.Add("Block", DynamicVars.Block.BaseValue);
                return line.GetFormattedText();
            }
        }

        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(5m, ValueProp.Move),
            new BlockVar(5m, ValueProp.Move),
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
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);   // 5 → 7（照抄本体铁斩波）
            DynamicVars.Block.UpgradeValueBy(2m);    // 5 → 7（照抄本体铁斩波）
        }
    }
}