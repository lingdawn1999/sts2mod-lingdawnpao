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

namespace lingdawnpao.Core
{
    /// <summary>
    /// 收网：2 费金卡，兑现器的**控制型**版本——与「张网以待」成对（张网待客 → 收网绞杀）。
    /// [gold]消耗[/gold]你[gold]伏击区域[/gold]中的**所有牌**；**每张被消耗的牌，使目标敌人的
    /// [gold]力量[/gold]降低 1 点**（升级 2 点），**同时你获得等量[gold]力量[/gold]**（表现为"偷取"）。
    /// 例：区域内 3 张牌 = 目标 -3 力量 **且自己 +3 力量**（升级 -6 / +6）。
    /// 区域上限 `AmbushZone.MaxSize = 3`（带「刺客」遗物为 4），所以一次结算的力量摆幅
    /// 最大是 3（升级 6）——双向合计 6 / 12 点，是金卡级的兑现。
    /// <para>
    /// **沿革**：初版是"每张换算成群体[gold]易伤[/gold] + [gold]虚弱[/gold]"（2026-09-23），
    /// **2026-09-24 用户重做为降力量**——两条减益都是**本回合有效**的消耗品，而力量是**永久**的：
    /// 把区域里的存量牌换成"敌方输出永久 -N"，是这条线上唯一能**跨回合**兑现的收益，
    /// 也让它从"铺场"变成"清账"（区域一次性清空 → 敌方攻势从此降档）。
    /// **2026-09-24 同日再加强为"偷取"**：用户判定"牌效和费用比差距太小"——
    /// 单纯减益只是把敌人的输出往下压，自己这边没有兑现，2 费 + 清空整个区域换 -3 力量不划算；
    /// 改成双向后同一次结算既削敌又滚自己的[gold]力量[/gold]，且**我方拿到的是永久增益
    /// （力量在本场战斗内持续，不随回合消失）**，与本流派"区域存量换永久收益"的定价一致
    /// （多张收网连续打出会滚起来）。
    /// **同日第三次定案：结算面从"全体敌人"收回"单体敌人"**（用户原话"偷全体有点变态了"）——
    /// 双向偷取挂在全体上收益翻倍（3 张牌＝全场 -3 **且**自己 +3），多目标战里等于一次结算吃 2N 份，
    /// 故改成 `TargetType.AnyEnemy`：只对**选中的那个敌人**扣力量，自己照常拿走等量的 N 点。
    /// **偷取是等量的、不设"敌方力量下限"保护**（不做 `min(敌方力量, N)`）：规则简单可预期，
    /// 敌方力量已被压成负数时本卡仍然照常给自己加——这是刻意留的滚雪球空间。
    /// </para>
    /// <para>
    /// **降力量的写法照本体**：`PowerCmd.Apply&lt;StrengthPower&gt;(..., -N, ...)`——本体「萎靡」
    /// `Malaise`（1 费金卡：X 费，给予敌人 -X 力量 + X 虚弱）与「命运同担」`SharedFate`（给敌人 -N 力量）
    /// 用的就是负值入参；`StrengthPower.AllowNegative => true`（已核对本体源码），
    /// 所以层数会真的压到负数并实时影响伤害计算，不需要自研"负力量"能力。
    /// </para>
    /// <para>
    /// **为什么收回单体也不撞车**：2026-09-22 那一版把区域张数结算到**全体**，是为了避开本体
    /// 「恶魔之焰」FiendFire（消耗**手牌**、每张打伤害）；现在改单体后依然不重合——本卡消耗的是
    /// **伏击区域**（mod 自有资源）而非手牌，换算出来的是[gold]力量[/gold]而不是伤害。
    /// **结算顺序**：先清空区域（`ExhaustAllAsync` 刻意不重算增益）、再叠力量、最后 `RefreshZoneAsync`——
    /// 与本流派其它兑现器同一条原则。
    /// 同族卡分工：「倾泄」触发牌本身效果、「重整计划」把牌换成手牌与能量、本卡把牌换成目标永久降力量 + 自身涨力量。
    /// </para>
    /// <para>
    /// **2026-09-25 用户加回自带的「消耗」词条，并把升级改成减费**。原话："基础 2 费带消耗，升级 1 费。
    /// （毕竟是永久偷取，防止无限滚雪球一直给 boss 抽干，减费提供润滑，普遍情况 1 费偷取 2 到 3 力量
    /// 已经金卡合模了，极端情况 1 费偷取 5 力量很变态，但也能作为配置出了整个构筑的奖励）"。
    /// 两条含义：① **消耗**堵死"多张收网连打、把力量滚起来"的老路（2026-09-24 曾刻意去掉消耗、
    /// 让它能反复打出，现在判定那条路会把 boss 的力量抽干）；② **升级不再加偷取量**（原为每张 1 → 2 点），
    /// 改成 `EnergyCost.UpgradeBy(-1)`，把升级的位置让给"润滑"——升级后仍是每张 1 点，
    /// 所以"1 费偷 2~3"是普遍形态、"1 费偷 5"要区域上限被抬到 5 才出现。
    /// </para>
    /// <para>
    /// ⚠️ 文案里的"消耗"指**伏击区域内的牌**，本卡自己的消耗走词条徽章——两处都写"消耗"是**刻意保留**的：
    /// 2026-09-25 用户只把后半句从"每张被消耗的牌"改成"每消耗一张牌"（原措辞语义不清），
    /// **前半句区域那句的"消耗"没动**。别再把其中一处换成别的说法；区域清空机制本身不变，
    /// 见 `AmbushZone.ExhaustAllAsync`。
    /// </para>
    /// </summary>
    public sealed class CloseTheNet : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<StrengthPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        /// <summary>2026-09-25 加回：见类注释里"防滚雪球"的那段。</summary>
        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<StrengthPower>(1m),
        ];

        public CloseTheNet()
            : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            int nets = await AmbushZone.ExhaustAllAsync(choiceContext, Owner);
            if (nets > 0)
            {
                decimal perNet = DynamicVars["StrengthPower"].BaseValue;
                await PowerCmd.Apply<StrengthPower>(choiceContext, cardPlay.Target, -nets * perNet, Owner.Creature, this);

                // 偷取：目标失去 N 点，自己拿走等量的 N 点（照本体「燃烧」Inflame 给自己加力量）
                await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, nets * perNet, Owner.Creature, this);
            }

            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 2 费 → 1 费（每张固定 1 点，升级不再加偷取量）
        }
    }
}