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
    /// 反复横跳：1 费罕见（蓝）**能力牌**。**当有牌加入[gold]伏击区域[/gold]时，获得 2 点格挡**（升级 3 点）。
    /// 定位：埋牌 = 叠甲的护甲引擎——与运转轴（雪原疾行/蓄能/翻雪的效果埋入）强联动，
    /// 每张效果埋都是 +2 甲；手动埋牌铺场时同样滚起来。带出 / 消耗 / 清空**不再触发**
    /// （2026-09-27 用户重做：触发从"双向变化"收窄为"仅加入"，数值 1/2 → 2/3）——
    /// 带出侧的收益归被带出的牌本身，倾泄/收网一次清空不再白送一大笔格挡。
    /// 与「有备而来」互补：那张吃"区域存量"，这张吃"区域流入"。
    /// <para>
    /// **沿革**：2026-09-24 用户定案技能牌 → 能力牌（整场战斗持续，原为当回合有效的技能）；
    /// 2026-09-27 用户重做触发与数值（见上）。结算点是 <see cref="AmbushZone.NotifyCountChangedAsync" />
    /// （埋入路径传入 +1，加入即结算；带出/清空传负值被忽略）。
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
            new PowerVar<FlipFlopPower>(2m),
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
            DynamicVars["FlipFlopPower"].UpgradeValueBy(1m); // 2 → 3 点
        }
    }
}