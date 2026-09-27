using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 棘背龙形态：**1 费蓝卡（Uncommon）技能**，**基础不带任何词条**；
    /// **升级给[gold]保留[/gold]**（升级收益是"能自己选兑现时机"，不是降费）。
    /// **移除你身上的所有[gold]冰冻[/gold]；每移除 1 层，给予**指定的一名敌人** 1 层[gold]撕裂[/gold]。**
    /// <para>
    /// **定位：冰冻线的"自身侧出口"**——此前全 mod 只有敌人侧的兑现器（「雪崩」把敌人的层数换成伤害、
    /// 「融冬」把敌人的层数变成不减的资源），而三张自伤牌（「冰封王座」「冰雪之躯」「冰碎瓦全」）
    /// 挂在自己身上的冰冻**只有代价、没有出口**，而且很黏：冰冻是**减半向上取整**（1 层永远留 1 层），
    /// 残留层数整局挂着，处决线（生命值不高于层数即死）对玩家同样生效。
    /// 本卡一次做完两件事：**把残留的债换成当回合的爆发**（撕裂是本体的回合内增伤，见 RendPower），
    /// **同时解除处决风险**。
    /// </para>
    /// <para>
    /// **为什么可以不带[gold]消耗[/gold]**：收益上限被"你身上真有多少冰冻"锁死——
    /// 打出后层数归零，同一张牌第二次抽到手上时基本没得换（除非期间又自伤过），
    /// 所以反复循环不构成套利（这一点与「神速」那种"消耗伏击区换额外回合"不同，那张必须靠[gold]消耗[/gold]兜底）。
    /// **不带[gold]保留[/gold]的代价是"抽到就得决定"**：手牌一满就得立刻兑现，往往只能在层数还薄的时候用掉；
    /// 升级补上[gold]保留[/gold] 之后才能攥着等大额兑现——这正是本卡的升级收益（用户 2026-09-24 定案）。
    /// </para>
    /// <para>
    /// **2026-09-24 用户整张重做**（原效果：0 费、本回合每打出一张[gold]攻击牌[/gold]就获得 1 层荆棘
    /// 并对随机敌人挂冰，由 `SpinosaurusFormPower` 承载，回合结束自解）。
    /// 重做的起因是上面那条缺口：用户先问"要不要做一张驱散自己冰冻 / 把冰冻变成收益的卡"，
    /// 随后定为**不新增卡、改现有弱联动卡**——选中的就是本卡（原是"卖形态"的临时能力，
    /// 与冰冻体系只有单向输出、没有回收）。原能力类 `SpinosaurusFormPower` 已随之删除。
    /// 同日第二轮：2 费 +【保留】【消耗】→ **1 费、无词条、升级给[gold]保留[/gold]**。
    /// </para>
    /// <para>
    /// **2026-09-25 用户第三次改（本次）**：收益从"[gold]荆棘[/gold]给自己"换成"**[gold]撕裂[/gold]给一名指定敌人**"
    /// （原话"撕裂下放给棘背龙形态，棘背龙形态的荆棘改成等同层数的指定单个敌人的撕裂"）。
    /// 两个变化：① 目标从 `Self` 改成 `AnyEnemy`，换来的层数打出去而不是留给自己；
    /// ② [gold]荆棘[/gold]（被攻击就反弹）在雪豹手里几乎吃不到（本来就靠格挡与冰冻拖节奏，
    /// 很少被主动攻击），而[gold]撕裂[/gold]是**当回合攻击增伤**——正好接上"清掉冰冻后立刻反打"。
    /// </para>
    /// </summary>
    public sealed class SpinosaurusForm : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
            HoverTipFactory.FromPower<RendPower>(),
        ];

        public SpinosaurusForm()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 先取层数（移除后 power 实例就没了），1 层换 1 层撕裂
            FrostbitePower? frost = Owner.Creature.GetPower<FrostbitePower>();
            int stacks = frost == null ? 0 : (int)frost.Amount;

            if (stacks > 0)
            {
                await PowerCmd.Remove(frost!);
                await PowerCmd.Apply<RendPower>(choiceContext, cardPlay.Target, stacks, Owner.Creature, this);
            }

            Log.Info($"棘背龙形态：移除自身 {stacks} 层冰冻 → 给予目标 {stacks} 层撕裂");
        }

        protected override void OnUpgrade()
        {
            AddKeyword(CardKeyword.Retain); // 升级：获得保留（照「决堤」的升级写法，费用与数值都不动）
        }
    }
}