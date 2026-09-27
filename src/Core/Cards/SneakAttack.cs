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
    /// 偷袭：埋伏机制的示例攻击牌，**基础卡（Basic）**——起始牌组 1 张，除开局外无获取途径
    /// （本体同类：猎人的中和/生存者；Basic 被本体的商店、战斗奖励、战斗内生成三处统一排除）。
    /// 直接打出：造成伤害并给予虚弱；从伏击区域**跨回合**带出打出：再加一段伤害与一层虚弱。
    /// **2026-09-23 埋伏体系大改**：判定从"被带出"改为"跨回合潜伏过"（见 <see cref="AmbushZone.IsVeteranAmbush" />）；
    /// 数值同时上调——基础 5 伤 → **6 伤 + 1 虚弱**（虚弱从"仅埋伏段"移到基础段，让直接打出也能立住），
    /// 埋伏从 +3 伤 +1 虚弱 → **+4 伤 +1 虚弱**（合计 10 伤 2 虚弱）。升级**只加伤害**：
    /// 基础 6 → 8、埋伏 4 → 6，虚弱固定 1（用户 2026-09-23 定案）。
    /// **2026-09-23 回削基础段**（用户定案）：基础伤害 6 → **5**（升级同步 8 → **7**，仍走同一条 `UpgradeValueBy(2m)`）；
    /// 埋伏段 4 / 6 保持不变 → 一次埋伏的合计伤害从 10 变成 **9**（2 层虚弱不变）。
    /// </summary>
    public sealed class SneakAttack : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<WeakPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(5m, ValueProp.Move),
            new DamageVar("AmbushDamage", 4m, ValueProp.Move),
            new PowerVar<WeakPower>(1m),
        ];

        public SneakAttack()
            : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 基础伤害与虚弱两种打法都生效；埋伏带出时额外补一段伤害与一层虚弱。
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);

            if (AmbushZone.IsVeteranAmbush(this))
            {
                var ambushDamage = (DamageVar)DynamicVars["AmbushDamage"];
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await DamageCmd.Attack(ambushDamage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                    await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);                       // 5 → 7（2026-09-23 基础段由 6 回削到 5）
            DynamicVars["AmbushDamage"].UpgradeValueBy(2m);              // 4 → 6（虚弱不随升级成长）
        }
    }
}
