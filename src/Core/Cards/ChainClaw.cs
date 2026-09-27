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
    /// 连环爪：1 费普通攻击，单体两段（2026-09-21 用户定稿）。
    /// 造成 5 点伤害 **2 次**；[gold]埋伏[/gold]（从伏击区域带出打出）：改为 **3 次**。
    /// 升级单段 5 → 7（照本体「双重打击」TwinStrike 的升级节奏：单段 +2）。
    /// **为什么要有「埋伏改 3 次」这一支**：初稿曾设想成"随机敌人多段"，但用户改成单打一个之后，
    /// 1 费 5 伤 ×2 就与本体「双重打击」TwinStrike（1 费白 · 单体 5 伤 ×2 · 升级单段 +2）
    /// **完全同款**。加上埋伏支之后，基础形态借用本体的平衡基准、兑现形态则长出本 mod 自己的东西，
    /// 与本体重合度降到只剩"1 费白单体两段"。
    /// **「伏兵」语义**：`AmbushTriggerCount` = 1 + 伏兵层数，作用于**整个埋伏分支**（照「扑杀」AmbushPounce
    /// 的写法）——所以有 1 层伏兵时是 3 段 ×2 遍 = 6 段，不是 4 段。
    /// 同族对照（单体多段）：本卡基础 2 段（1 费）｜收网 `CloseTheNet` 按区域张数多段（2 费金）｜
    /// 猛虎连击 `TigerCombo` 带出连锁（2 费金）。
    /// </summary>
    public sealed class ChainClaw : CardModel, IAmbushBonusCard
    {
        private const int BaseHits = 2;

        private const int AmbushHits = 3;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(5m, ValueProp.Move),
        ];

        public ChainClaw()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 埋伏带出：3 段一支，「伏兵」让这一支整体重复触发。
            // 2026-09-23 埋伏大改：判定改为"跨回合潜伏过"（当回合埋了马上带出不给 3 段，走基础 2 段）。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(AmbushHits).FromCardCompat(this, cardPlay)
                        .Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }

                return;
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(BaseHits).FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m); // 每段 5 → 7（照本体双重打击）
        }
    }
}