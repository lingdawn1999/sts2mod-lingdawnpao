using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 倾泄：**1 费**基础技能（Basic，2026-09-22 由 0 费改成 1 费）——开局自带，除开局外无获取途径
    /// （本体同类：猎人的中和/生存者；Basic 被本体的商店、战斗奖励、战斗内生成三处统一排除）。
    /// **不带消耗**（2026-09-22 用户改：原来自带消耗"太影响运转了"，去掉；同时把费用 0 → 1 作为补偿）。
    /// **升级获得[gold]保留[/gold]**（原为"每张格挡 +1"）。
    /// 打出时带出伏击区域的所有牌（逐张触发其埋伏效果），目标跟随本牌选择，传给带出的攻击牌。
    /// 打出时按伏击区域**当前张数**每张获得格挡（基础 2）。
    /// 结算顺序刻意是「先算格挡 → 再带出」：区域被抽空后算就只能拿 0，且现在这样区域给的 +敏捷
    /// 也吃得上（那份敏捷随区域清空本来就会消失）。总数走本体的计算值体系
    /// （CalculationBase 0 + CalculationExtra 每张值 × 区域张数，同本体 Mirage/Stack 的写法），
    /// 所以卡面预览显示的就是实战能拿到的数。
    /// </summary>
    public sealed class HuntOperation : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CalculationBaseVar(0m),
            new CalculationExtraVar(2m),
            new CalculatedBlockVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? _) => AmbushZone.For(card.Owner).Count),
        ];

        public HuntOperation()
            : base(1, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 先按"区域还满着"的张数把格挡拿走，再带出（带出会逐张抽空区域）
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.CalculatedBlock.Calculate(cardPlay.Target), DynamicVars.CalculatedBlock.Props, cardPlay);

            // 带出的牌经 AutoPlay 打出（IsAutoPlay=true），不会再触发伏击者的带出，无连锁
            await AmbushZone.BringOutAllAsync(choiceContext, cardPlay.Target, Owner);
        }

        protected override void OnUpgrade()
        {
            AddKeyword(CardKeyword.Retain); // 升级：保留（每张格挡不再随升级 +1）
        }
    }
}