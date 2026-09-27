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
    /// 猛虎连击：造成伤害后带出伏击区最前的一张牌；只有带出的是攻击牌时才"再次打出此牌"。
    /// 循环即递归语义：每轮先打伤害再带出，带出攻击牌则继续下一轮；非攻击牌照常带出但连击终止，
    /// 伏击区清空时再打最后一段后终止（N 张全攻击 = N+1 段伤害）。升级：伤害 12 → 16。
    /// <para>
    /// **2026-09-25 用户移除[gold]撕裂[/gold]**：原为"先挂撕裂（基础 2，升级 4）再打伤害"，
    /// 与多段攻击构成"每段都吃撕裂加成"的本命组合。移除的理由是撕裂**整条线被下放给
    /// 「棘背龙形态」**（那张卡改成"移除自身冰冻 → 给指定敌人等量撕裂"），
    /// 避免两处重复承载同一个机制；本卡保留"多段 + 带出连锁"这半边。
    /// </para>
    /// </summary>
    public sealed class TigerCombo : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(12m, ValueProp.Move),
        ];

        public TigerCombo()
            : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

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
            DynamicVars.Damage.UpgradeValueBy(4m); // 12 → 16（撕裂已移除，升级只加伤害）
        }
    }
}
