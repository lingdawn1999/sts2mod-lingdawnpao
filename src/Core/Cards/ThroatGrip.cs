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
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 锁喉：2费罕见攻击，造成12（16）点伤害，给予2层易伤。
    /// 埋伏额外给予1层易伤，造成8（11）点伤害；十面埋伏仅收集额外部分。
    /// </summary>
    public sealed class ThroatGrip : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<VulnerablePower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(12m, ValueProp.Move),
            new DamageVar("AmbushDamage", 8m, ValueProp.Move),
            new PowerVar<VulnerablePower>(2m),
            new PowerVar<VulnerablePower>("AmbushVulnerable", 1m),
        ];

        public ThroatGrip()
            : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);

            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                var ambushDamage = (DamageVar)DynamicVars["AmbushDamage"];
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target,
                        DynamicVars["AmbushVulnerable"].BaseValue, Owner.Creature, this);
                    await DamageCmd.Attack(ambushDamage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(4m);          // 12 → 16
            DynamicVars["AmbushDamage"].UpgradeValueBy(3m); // 8 → 11
        }
    }
}
