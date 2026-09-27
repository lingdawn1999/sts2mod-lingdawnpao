using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪拥：**3 费金卡技能，自带[gold]消耗[/gold]**。**敌方每拥有一点[gold]冰冻[/gold]，
    /// 你就获得 1 点[gold]覆甲[/gold]**（升级后每点给 2）。
    /// <para>
    /// **2026-09-24 用户重做**（原话："雪拥改成3费，敌方每拥有一点冰冻，就获得1点覆甲（升级给2）消耗"）：
    /// 原版是"按抽牌堆张数给格挡 + 洗牌"的通用防御件（通篇沿革见 git 历史与卡表），
    /// 现在改成**冰冻体系的兑现口**——把铺在敌人身上的冰冻从"只用来算伤害"变成"能换成持续防御"。
    /// 定位：冰流中后期的一张**防守转换件**，冰冻铺得越多这一下越厚。
    /// </para>
    /// <para>
    /// **为什么自带[gold]消耗[/gold]**：覆甲是**每回合白给格挡**的持久收益（回合结束给格挡、回合开始减 1 层），
    /// 一张能反复回手的"层数转化器"会让冰流同时拿到爆发与长线防御，故一轮只能用一次。
    /// **费用 1 → 3**：原费用是给"抽牌堆格挡"定的；现在它按全场冰冻总量一次兑现（群战里可叠得很高），
    /// 3 费才对得上这个上限。
    /// </para>
    /// <para>
    /// **覆盖范围 = 场上所有敌人之和**（`HittableEnemies` 逐个累加冰冻层数）——卡是自身目标，
    /// 所以"敌方"取全体而非单体；群战时层数会相加，这是本卡在群战里的强势点。
    /// </para>
    /// <para>
    /// **冰冻层数的口径**：读 `FrostbitePower.Amount`，也就是**当前残留层数**——本体的挂冰会立即
    /// 按当前层数结算一次伤害并把层数减半（`LayersRetainedDivisor = 2`），所以这里读到的是减半后的值，
    /// 不是"历史挂过的总量"。这是刻意的（与卡面"敌军身上那个冰冻数字"完全一致，玩家看得懂）。
    /// </para>
    /// <para>
    /// 升级收益走**数值**（每点冰冻给的覆甲 1 → 2），**不再动费用**，守住"降费与数值二选一"的约定。
    /// </para>
    /// </summary>
    public sealed class Snowdrift : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<PlatingPower>(),
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        /// <summary>「覆甲」动态变量 = **每点冰冻兑换的覆甲层数**（卡面显示的就是这个兑换率）。</summary>
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<PlatingPower>(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public Snowdrift()
            : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            IReadOnlyList<Creature>? enemies = Owner.Creature.CombatState?.HittableEnemies;
            if (enemies == null)
            {
                return;
            }

            decimal frost = 0m;
            foreach (Creature enemy in enemies)
            {
                frost += enemy.GetPower<FrostbitePower>()?.Amount ?? 0m;
            }

            decimal plating = frost * DynamicVars["PlatingPower"].BaseValue;
            if (plating <= 0m)
            {
                return;
            }

            await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature, plating, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["PlatingPower"].UpgradeValueBy(1m);   // 每点冰冻给的覆甲 1 → 2
        }
    }
}