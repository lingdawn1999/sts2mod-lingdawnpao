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
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>两段攻击；每次埋伏提供本回合内的一次免费攻击，可累计次数。</summary>
    public sealed class ChainClaw : CardModel, IAmbushBonusCard
    {
        private const int BaseHits = 2;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
            HoverTipFactory.FromPower<ChainClawDiscountPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(6m, ValueProp.Move), // 2026-09-28 前期补强：单段 5 → 6（升级 8）
            new EnergyVar(1), // 卡面0费后的能量图标。
        ];

        public ChainClaw()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(BaseHits).FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                await PowerCmd.Apply<ChainClawDiscountPower>(choiceContext, Owner.Creature,
                    AmbushZone.AmbushTriggerCount(Owner), Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m); // 每段 6 → 8
        }
    }
}
