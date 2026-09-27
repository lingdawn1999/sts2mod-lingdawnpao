using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 反复横跳：1 费罕见（蓝）**能力牌**。伏击区域的牌数每变化 1 张，你获得 1 点格挡（升级 2 点）。
    /// 定位：把伏击流的"埋 ↔ 带出"这种来回节奏直接变成护甲——埋牌铺场时叠甲、倾泄/收网清空时再叠一次，
    /// 双向都算（这也是"横跳"名字的由来）。结算点是 AmbushZone 的四处增删（埋入 / 生成埋入 / 带出 / 清空）。
    /// 与「有备而来」互补：那张吃"区域存量"，这张吃"区域流量"。
    /// <para>
    /// **2026-09-24 用户定案：技能牌 → 能力牌**（原话"反复横跳太弱鸡了，改成能力"）。
    /// 原设计是"本回合内有效"的一次性技能（<see cref="FlipFlopPower" /> 曾在回合结束时自删），
    /// 单独打出等于空过、必须先铺好区域才有收益，作为蓝卡回报偏低；改成能力牌后**整场战斗持续生效**，
    /// 从"一次性爆发"变成"越滚越厚的引擎件"，与本流派"区域来回变动"的主循环绑得更自然。
    /// **费用、稀有度、数值均未改动**（1 费 / 罕见 / 1 点、升级 2 点）。
    /// </para>
    /// </summary>
    public sealed class FlipFlop : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FlipFlopPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FlipFlopPower>(1m),
        ];

        public FlipFlop()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<FlipFlopPower>(choiceContext, Owner.Creature,
                DynamicVars["FlipFlopPower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FlipFlopPower"].UpgradeValueBy(1m); // 1 → 2 点
        }
    }
}