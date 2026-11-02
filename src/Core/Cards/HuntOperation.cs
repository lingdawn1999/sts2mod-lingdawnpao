using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
    /// 升级：每张牌的基础格挡2→3，并获得保留。
    /// 打出时带出伏击区域的所有牌（逐张触发其埋伏效果），目标跟随本牌选择，传给带出的攻击牌。
    /// 按结算前的区域牌数一次获得每张2（3）点基础格挡，敏捷仅加一次，再带出所有牌。
    /// 空区或区域失效时不产生格挡，沿用原有区域可用条件。
    /// </summary>
    public sealed class HuntOperation : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CalculationBaseVar(0m),
            new CalculationExtraVar(2m),
            new CalculatedBlockVar(ValueProp.Move).WithMultiplier((card, _) => AmbushZone.For(card.Owner).Count),
        ];

        public HuntOperation()
            : base(1, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            var zone = AmbushZone.For(Owner);
            if (zone.Count == 0 || zone.IsCoolingDown || !AmbushZone.CanContinueCombat(Owner)) return;
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.CalculatedBlock.Calculate(null),
                DynamicVars.CalculatedBlock.Props, cardPlay);
            await AmbushZone.BringOutAllAsync(choiceContext, cardPlay.Target, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.CalculationExtra.UpgradeValueBy(1m);
            AddKeyword(CardKeyword.Retain);
        }
    }
}
