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
    /// 猛虎连击：先给予目标撕裂（基础 1 层，升级 2 层），然后造成伤害并带出伏击区最前的一张牌；
    /// 只有带出的是攻击牌时才"再次打出此牌"。
    /// 循环即递归语义：每轮先打伤害再带出，带出攻击牌则继续下一轮；非攻击牌照常带出但连击终止，
    /// 伏击区清空时再打最后一段后终止（N 张全攻击 = N+1 段伤害）。
    /// 撕裂在循环外只挂一次，但每段攻击伤害各触发一次（N+1 段 = 撕裂吃 N+1 次），多段与撕裂天然联动。
    /// <para>
    /// **撕裂沿革**：2026-09-25 用户移除（当时整条撕裂线下放给「棘背龙形态」，避免重复承载）；
    /// **2026-09-27 用户定案回归**（1/2 层）——升级位从"伤害 12 → 16"改为"撕裂 1 → 2"，
    /// 升级不再只给白值，且撕裂×多段的本命组合是这张卡的正反馈来源。
    /// </para>
    /// </summary>
    public sealed class TigerCombo : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<RendPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(12m, ValueProp.Move),
            new PowerVar<RendPower>(1m),
        ];

        public TigerCombo()
            : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 撕裂在循环外挂一次：每段攻击伤害各触发一次，N+1 段全吃加成
            await PowerCmd.Apply<RendPower>(choiceContext, cardPlay.Target,
                DynamicVars["RendPower"].BaseValue, Owner.Creature, this);

            AmbushZoneState zone = AmbushZone.For(Owner);
            while (true)
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                // 先打伤害再判定：伏击区已空 = 没有牌可带出，最后一段打完即止
                if (zone.Count == 0)
                {
                    break;
                }

                CardModel? broughtOut = await AmbushZone.BringOutFirstAsync(choiceContext, cardPlay.Target, Owner);
                // 带出的是攻击牌才"再次打出此牌"；技能/能力牌照常带出结算，但连击到此为止
                if (broughtOut == null || broughtOut.Type != CardType.Attack)
                {
                    break;
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars["RendPower"].UpgradeValueBy(1m); // 撕裂 1 → 2 层（2026-09-27 用户定案，替代原"伤害 12 → 16"）
        }
    }
}
