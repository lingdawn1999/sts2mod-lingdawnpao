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
    /// 覆霜：给目标挂 5 层冰冻（升级 8）。挂冰瞬间结算等同层数的直伤（**可被格挡**）并 **-2 层**
    /// （5 层=立即 5 伤留 3 层；8 层=立即 8 伤留 6 层）。残留层数配合
    /// "生命值低于冰冻层数立即死亡"提供斩杀线：后续挂冰叠层或补刀都可能触发处决。
    /// </summary>
    public sealed class FrostCoat : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(5m),
        ];

        public FrostCoat()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target, DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(3m); // 冰冻 5 → 8
        }
    }
}
