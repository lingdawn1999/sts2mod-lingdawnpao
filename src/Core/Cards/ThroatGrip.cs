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
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 锁喉：2 费罕见攻击，12 点伤害（升级 16）+ 2 层易伤——伏击流的"减益锁头"。
    /// 易伤放在**基础效果**而不是埋伏分支：它是本流派最缺的乘区，埋进去被倾泄/猛虎连击
    /// 多段带出时，后续每张带出牌都吃 50% 加成；放基础则单卡就能先手挂上，不必先埋伏。
    /// 埋伏（**跨回合**从伏击区域带出打出）：额外造成 8 点伤害（升级 11）——给的是"兑现"。
    /// **2026-09-23 埋伏体系大改**：判定从"被带出"改为"跨回合潜伏过"
    /// （见 <see cref="AmbushZone.IsVeteranAmbush" />），埋伏伤害同步从 +6 上调到 **+9**（升级 +12）——
    /// 代价从"当回合切形态白嫖"变成"真等一回合 + 占格"，收益随之抬高。
    /// **2026-09-23 用户再调**：基础 11 → **12**（升级 15 → 16）、埋伏 9 → **8**（升级 12 → 11）
    /// ——**埋伏合计不变（仍 20 伤）**，只是把 1 点伤害从"埋伏段"挪到"基础段"，
    /// 让不埋、直接打的用法也好用一点；升级幅度沿用原来的 +4 / +3。
    /// 刻意不挂虚弱：初始牌组的偷袭已带虚弱，再添常驻来源会让虚弱浓度过高。
    /// </summary>
    public sealed class ThroatGrip : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<VulnerablePower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(12m, ValueProp.Move),
            new DamageVar("AmbushDamage", 8m, ValueProp.Move),
            new PowerVar<VulnerablePower>(2m),
        ];

        public ThroatGrip()
            : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);

            if (AmbushZone.IsVeteranAmbush(this))
            {
                var ambushDamage = (DamageVar)DynamicVars["AmbushDamage"];
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await DamageCmd.Attack(ambushDamage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(4m);          // 12 → 16
            DynamicVars["AmbushDamage"].UpgradeValueBy(3m); // 8 → 11
        }
    }
}