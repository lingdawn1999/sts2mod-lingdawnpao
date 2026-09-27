using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击本能（**改名前叫「猎杀本能」**）：2 费金卡能力（沿用卡图，只换机制与显示名）。
    /// **每回合首次**把牌加入[gold]伏击区域[/gold]时获得 1 点[gold]能量[/gold]；
    /// **每回合首次**从伏击区域带出牌时抽 1 张牌（**升级：摸牌 1 → 2**）。
    /// ⚠️ **改名只动显示名**：类名仍叫 `KillerInstinct`、本地化 key 仍是 `KILLER_INSTINCT`、
    /// 卡面文件仍是 `packed/card_portraits/lingdawnpao/killer_instinct.png`（照「深咬」→「断腕」、
    /// 「吞噬」→「神速」的先例，避免牵动本地化 key 与卡面文件名）。
    /// <para>
    /// **定位：把"埋进 → 带出"这个基本循环本身变成运转收益**——碎片化的伏击操作从此每回合回收
    /// 一笔费用和一张牌，是整条线的"稳定加费 + 稳定过牌"双补件。
    /// </para>
    /// <para>
    /// **2026-09-23 用户整张重做**（原版：每当你把一张牌加入伏击区域，就对随机敌人造成 6 点伤害，
    /// 升级 8；对标本体 2 费金卡 Juggernaut）。重做的起因是用户诊断
    /// "**缺乏稳定加费卡和稳定过牌卡**"——一次性 +1 费（「蓄能」）杯水车薪，需要的是像本体
    /// 「薪火之源」那样**每回合都到账**的东西，于是把这张重叠度最高的金卡（埋伏线里第三张
    /// "伤害放大件"，与「蓄势」「暗夜收割」同轴）腾出来改做引擎，**没有新增卡图**。
    /// </para>
    /// <para>
    /// **对标**：本体要两张三费才凑齐这两样——「薪火之源」`Pyre` 2 费金（能量上限 +1/回合）+
    /// 「机器学习」`MachineLearning` 1 费金（每回合多抽 1 张）。本卡 2 费一次给全，这是它"偏强"的来源；
    /// 唯一的强度约束是"两条触发都必须靠伏击区的实际动作"（进牌、出牌），而这动作本身要花费用和牌。
    /// ⚠️ **升级只买摸牌数，绝不降费**（用户 2026-09-23 明确："白板就价值 3 费了，再给减费过分"）：
    /// 用户同时判定**这张卡"已经很超模"**，所以升级档只把摸牌 1 → 2，不做费用 2 → 1。
    /// **若后续要收口，优先动触发频率**（把其中一项改成"每两回合一次"），而不是动费用或摸牌数。
    /// </para>
    /// <para>
    /// ⚠️ **"每回合首次"是防刷的唯一闸门**：埋牌与带出在一回合内都能发生多次（「设伏」一次埋 2 张、
    /// 「倾泄」一次带出一串、切形态还能反复倒手），按次数结算必被免费燃料刷爆——这正是「暗夜收割」
    /// 前两版的死因（雪球/晕眩批量喂）。详见 <see cref="KillerInstinctPower" />。
    /// </para>
    /// </summary>
    public sealed class KillerInstinct : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<KillerInstinctPower>(),
            EnergyHoverTip,
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new EnergyVar(1),
            new CardsVar(1),
        ];

        public KillerInstinct()
            : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            // 能力层数承载"每回合首次带出摸几张"；能量固定为 KillerInstinctPower.EnergyGain
            await PowerCmd.Apply<KillerInstinctPower>(choiceContext, Owner.Creature,
                DynamicVars.Cards.BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Cards.UpgradeValueBy(1m); // 摸牌 1 → 2
        }
    }
}