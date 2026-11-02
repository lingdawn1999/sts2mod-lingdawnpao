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
    /// 偷袭：1费初始攻击，造成6（8）点伤害，给予1（2）层虚弱。
    /// 埋伏额外给予1层虚弱，造成4（6）点伤害；十面埋伏仅收集这一额外部分。
    /// </summary>
    public class SneakAttack : CardModel, IAmbushBonusCard
    {
        protected virtual decimal InitialDamage => 6m;
        protected virtual decimal InitialAmbushDamage => 4m;
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<WeakPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(InitialDamage, ValueProp.Move),
            new DamageVar("AmbushDamage", InitialAmbushDamage, ValueProp.Move),
            new PowerVar<WeakPower>(1m),
            new PowerVar<WeakPower>("AmbushWeak", 1m),
        ];

        public SneakAttack() : this(1, CardRarity.Basic)
        {
        }

        protected SneakAttack(int cost, CardRarity rarity)
            : base(cost, CardType.Attack, rarity, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 基础伤害与虚弱两种打法都生效；埋伏带出时额外补一段伤害与一层虚弱。
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);

            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                var ambushDamage = (DamageVar)DynamicVars["AmbushDamage"];
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target,
                        DynamicVars["AmbushWeak"].BaseValue, Owner.Creature, this);
                    await DamageCmd.Attack(ambushDamage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);                       // 基础段 6 → 8
            DynamicVars["AmbushDamage"].UpgradeValueBy(2m);              // 埋伏段 4 → 6，额外虚弱固定1
            DynamicVars["WeakPower"].UpgradeValueBy(1m);                 // 虚弱 1 → 2（2026-09-28 用户定稿）
        }
    }
}
