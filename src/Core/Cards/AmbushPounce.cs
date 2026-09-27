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
    /// 扑杀：1 费罕见攻击，8 点伤害（升级 12）。
    /// 埋伏（**跨回合**从伏击区域带出打出）：伤害范围**改为全体**，且伤害额外 +2——
    /// 不是追加一段，而是把原本那 8 点（+2）打向所有敌人。
    /// 定位是伏击流的群体解，代价是埋进去时要付 1 费且占一格、并**至少等一个回合**才兑现。
    /// **2026-09-23 埋伏体系大改**：① 判定从"被带出"改为"跨回合潜伏过"
    /// （见 <see cref="AmbushZone.IsVeteranAmbush" />，当回合埋了马上带出只走单体基础伤害）；
    /// ② 埋伏收益上调——改全体之外再加 +2 伤（`AmbushBonus`，不随升级成长，所以升级后是 12+2=14）。
    /// 本体已有同名卡 Pounce（1 费"下一个技能费用为 0"），类名加 Ambush 前缀避免卡牌 ID 冲突。
    /// </summary>
    public sealed class AmbushPounce : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(8m, ValueProp.Move),
            new DamageVar("AmbushBonus", 2m, ValueProp.Move),
        ];

        public AmbushPounce()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 埋伏带出时不吃选定目标，直接打全体（「伏兵」会让这一支重复触发）
            if (AmbushZone.IsVeteranAmbush(this))
            {
                decimal allDamage = DynamicVars.Damage.BaseValue + DynamicVars["AmbushBonus"].BaseValue;
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await DamageCmd.Attack(allDamage).FromCardCompat(this, cardPlay)
                        .TargetingAllOpponents(CombatState!)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }

                return;
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(4m); // 8 → 12（埋伏段 = 基础 + AmbushBonus 2 = 14）
        }
    }
}