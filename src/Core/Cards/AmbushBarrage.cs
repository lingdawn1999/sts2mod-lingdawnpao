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
    /// 十万埋伏：**2 费金卡攻击**。造成伤害——伤害 = **基础值 + 每层成长 × 本场战斗因伏击造成伤害的次数**。
    /// <para>
    /// **2026-09-24 新增、同日改版（用户定案）**：仿蕾忍「十亿响刀落」/酒狐「剑术大师」那种"输出终端"，
    /// 用来补本 mod 的**金卡攻击浓度**（此前金卡里攻击件偏少、体系件偏多）。
    /// 初版是"挂一个能力，伏击牌每造成一次伤害就随机再打一下"，用户改为**伤害成长型**：
    /// 原话"造成x点伤害。本场战斗你每因伏击造成过一次伤害，此牌伤害增加y"。
    /// </para>
    /// <para>
    /// **两版的差别**（改版后旧的「十万埋伏」能力已删）：
    /// 旧版是"**持续输出**"（能力在场时每一次伏击伤害都额外打一下，赚的是攻击次数）；
    /// 新版是"**单点爆发**"（把已积累的伏击战绩一次性折算成一张大攻击，赚的是这一张的伤害）。
    /// 新版更符合"金卡终端"的节奏——玩家一路铺垫、最后一击收尾，而不是开局先打它铺能力。
    /// 记账点见 `FrostFang.AfterDamageGiven`，字段是 `AmbushZoneState.AmbushDamageCount`。
    /// </para>
    /// <para>
    /// **动态伤害走本体标准三件套**（照本体「完美打击」<c>PerfectedStrike</c>）：
    /// `CalculationBaseVar`（基础）+ `ExtraDamageVar`（每层成长）+ `CalculatedDamageVar.WithMultiplier`（层数来源）。
    /// 这样卡面显示的是**实时算好的伤害数字**（手牌里就能看到它随着伏击战绩变大），
    /// 而且打出时由 `DamageCmd.Attack(DynamicVars.CalculatedDamage)` 直接取算好的值，不需要手写乘法。
    /// </para>
    /// <para>
    /// ⚠️ **`Owner` 不判空**：与本体 `PerfectedStrike` 一致，卡面预览时框架保证卡有 owner
    /// （预览回调本身就是"有归属才调"），多一层判空反而掩盖真问题。
    /// </para>
    /// <para>
    /// 数值：基础 8、每层成长 4；**升级基础 12 + 每层成长 5**（2026-09-27 用户定案：
    /// "升级改成打 12，每层成长 +5"——基础与成长轴同时抬，替代此前"只升基础"与"只升成长"两个中间版）。
    /// </para>
    /// </summary>
    public sealed class AmbushBarrage : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CalculationBaseVar(8m),
            new ExtraDamageVar(4m),
            new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) =>
                AmbushZone.AmbushDamageCount(card.Owner)),
        ];

        public AmbushBarrage()
            : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.CalculationBase.UpgradeValueBy(4m); // 基础 8 → 12（2026-09-27 用户定案）
            DynamicVars.ExtraDamage.UpgradeValueBy(1m);     // 每层成长 4 → 5
        }
    }
}