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
    /// 豹咬：2 费普通技能，挂 8 层冰冻（升级 11），自带保留。
    /// 与覆霜（1 费 5 层、立即用）互补：豹咬费用高、层数厚，可以一直留在手牌里，
    /// 等冰冻层数把目标压进处决线（HP < 层数）时再咬下致命一口。
    /// **2026-09-24 用户加强：7/10 → 9/13**（升级增量 +3 → +4）。
    /// 起因：用户判定冰冻体系前期太弱（挂冰牌多为技能牌、费用换不来即时压制），
    /// 先把这张"厚层数"的通用挂冰件抬上来，同时给全体系加了"对敌挂冰无视格挡"的双标（见 FrostbitePower）。
    /// **2026-09-25 用户回调：9/13 → 8/11**（基础 −1 层，升级增量 +4 → +3）——削弱 1 点基础冰冻。
    /// </summary>
    public sealed class LeopardBite : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(8m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

        public LeopardBite()
            : base(2, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target,
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(3m); // 冰冻 8 → 11
        }
    }
}
