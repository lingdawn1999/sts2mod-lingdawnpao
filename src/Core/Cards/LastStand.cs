using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 背水一战：1 费普通（白）攻击，造成 14 点伤害（升级 20）。
    /// 代价是**打完本回合伏击区进入冷却**（不能再埋牌/带出、不提供力/敏，下个玩家回合开始恢复）。
    /// <para>
    /// **2026-09-24 用户复改**：① 删掉"只有当[gold]伏击区域[/gold]为空时才能打出"这个前置条件
    /// （原为 `IsPlayable` 覆盖 + `ShouldGlowGoldInternal`，照本体 `Clash` / `GrandFinale` 的写法）——
    /// 理由是本回合区域冷却的代价已经很重、再加一条门槛只是让卡面更长，玩家也不好看懂；
    /// 两个覆盖都删掉，回到 `CardModel` 的默认（`IsPlayable => true`、不发光）。
    /// ② 伤害 11 → **14**（升级 17 → **20**，升级增量仍是 +6）。
    /// </para>
    /// 实现与「凝神」同一条冷却路径：`zone.IsCoolingDown = true` + 复用 `ConcentrationPower`
    /// （回合开始解除冷却并自解，否则区域会永久卡在冷却）。
    /// </summary>
    public sealed class LastStand : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(14m, ValueProp.Move),
        ];

        public LastStand()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 负面效果（2026-09-22 用户加）：打完本回合伏击区进入冷却——
            // 本回合不能再埋牌、不能带出、也不提供力/敏，下个玩家回合开始时恢复。
            // **必须配一个"回合开始解除冷却"的记账能力**：全项目只有 OverloadPower 会把
            // IsCoolingDown 归零，但它管的是"过载 → 冷却 → 恢复"两回合状态机；这里直接复用
            // 凝神当初为此建的 ConcentrationPower（同一条冷却、同一种当回合生效），结构照「凝神」写。
            AmbushZoneState zone = AmbushZone.For(Owner);
            zone.IsCoolingDown = true;
            await PowerCmd.Apply<ConcentrationPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            Log.Info("伏击区域：背水一战，本回合失效");
            await AmbushZone.RecomputeBonusesAsync(choiceContext, Owner);
            AmbushZoneUi.Refresh();
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(6m); // 14 → 20
        }
    }
}