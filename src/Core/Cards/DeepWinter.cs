using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 深冬：1 费罕见（蓝）技能。给予 5 层冰冻；**如果目标已有冰冻，则改为给予 8 层**（升级 6 / 10）。
    /// 阶梯式挂冰：先手牌（霜息/冰锥/覆霜）垫出层数，再用它加量——奖励"先铺后加"的顺序，
    /// 而不是无脑单卡堆层。
    /// </summary>
    public sealed class DeepWinter : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(5m),
            new DynamicVar("DeepWinterBoost", 8m),
        ];

        public DeepWinter()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            decimal stacks = cardPlay.Target.GetPower<FrostbitePower>() != null
                ? DynamicVars["DeepWinterBoost"].BaseValue
                : DynamicVars["FrostbitePower"].BaseValue;
            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target, stacks, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(2m);  // 常态 5 → 7（2026-09-24 升级收益 +1 → +2）
            DynamicVars["DeepWinterBoost"].UpgradeValueBy(2m); // 已有冰冻 8 → 10
        }
    }
}