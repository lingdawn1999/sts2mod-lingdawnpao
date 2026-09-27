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
    /// 追猎：1 费蓝卡**攻击**。造成伤害并给予目标 2 层[gold]追猎[/gold]——
    /// 被追猎的敌人会成为**从伏击区域带出的牌的锁定目标**，且那些牌打它时伤害 ×1.25（见 <see cref="TrackedPower" />）。
    /// 升级：只加伤害（6 → 9）。
    /// <para>
    /// **2026-09-24 重做（用户定案）**：原版这张是「技能 + 移除目标所有增益 + 1 层追猎」，
    /// 自带【固有】【消耗】。重做后的定案是：
    /// ① "移除所有增益"曾拆出去独立成一张卡（破势 → 消融 → 拟态，中途重做过"复制增益"），
    /// **该卡已于 2026-09-25 整张删除**（用户判定"这卡有点原因不明了"）；
    /// ② 本卡改为**攻击牌**（伤害 + 锁定），**去掉【固有】、去掉【消耗】**；
    /// ③ 追猎**基础就给 2 层**（原先要升级才有 2 层），升级收益改为**只加伤害**。
    /// </para>
    /// <para>
    /// ⚠️ **追猎的层数是有意义的**：`TrackedPower` 的层数 = **持续回合数**（敌方回合结束递减），
    /// 所以基础 2 层 = 锁两轮。这条曾经在卡表的能力描述里漏写，导致一度误判成"升级无收益"。
    /// 本体已有同名卡 `Tracking`（2 费金卡、对虚弱敌人伤害翻倍），故类名用 `Track`、
    /// 能力用 `TrackedPower` 避开。
    /// </para>
    /// </summary>
    public sealed class Track : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<TrackedPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(6m, ValueProp.Move),
            new PowerVar<TrackedPower>(2m),
        ];

        public Track()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 先打伤害再上标记：标记是给**之后**带出的牌用的
            await PowerCmd.Apply<TrackedPower>(choiceContext, cardPlay.Target,
                DynamicVars["TrackedPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(3m); // 6 → 9（2026-09-24 重做：升级只加数值）
        }
    }
}