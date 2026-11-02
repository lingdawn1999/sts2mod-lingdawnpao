using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>吞牌成长归自身；埋伏基础扣1血、打7（9）伤害，十面埋伏不继承吞牌成长。</summary>
    public sealed class FrostFangCard : CardModel, IAmbushBonusCard
    {
        private decimal _extraDamage;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new HpLossVar(1m),
            new DamageVar(5m, ValueProp.Move),
            new DamageVar("AmbushDamage", 7m, ValueProp.Move),
        ];

        private decimal ExtraDamage
        {
            get => _extraDamage;
            set
            {
                AssertMutable();
                _extraDamage = value;
            }
        }

        public FrostFangCard()
            : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // **自选**要烧的那张（2026-09-25 用户要求：从"固定烧最先加入的"放宽为自选）；
            // 区域为空时返回 null，那就只打基础伤害。区域堆是 PileType.None，
            // 走不了 FromCombatPile（非战斗堆会抛），见 AmbushZone.SelectOneAsync 的说明。
            CardModel? swallowed = await AmbushZone.SelectOneAsync(choiceContext, Owner, SelectionScreenPrompt);

            // 读**选中那张**的卡面伤害——烧掉之后那张牌就不在区域里了。
            // X 同时加到两个伤害数字上（X 双吃：带出打出的总伤 = (基础+X) + (埋伏段+X)）。
            decimal added = 0m;
            if (swallowed != null)
            {
                if (swallowed.DynamicVars.ContainsKey("Damage"))
                {
                    added += swallowed.DynamicVars.Damage.BaseValue;
                }
                else if (swallowed.DynamicVars.ContainsKey("CalculatedDamage"))
                {
                    added += swallowed.DynamicVars.CalculatedDamage.Calculate(null);
                }
            }

            if (added > 0m)
            {
                DynamicVars.Damage.BaseValue += added;
                ((DamageVar)DynamicVars["AmbushDamage"]).BaseValue += added;
                ExtraDamage += added;
            }

            if (swallowed != null)
            {
                await AmbushZone.ExhaustChosenAsync(choiceContext, Owner, swallowed);
            }

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 埋伏段：只有**跨回合**从伏击区域带出打出时才触发（2026-09-23 埋伏大改，见 IsVeteranAmbush）。
            // **代价也只在这一段里付**（2026-09-22 用户更正）：正常从手牌打出只造成伤害、不扣血；
            // 带出打出才是"额外一段伤害 + 失去 1 点生命"的代价（2026-09-23 曾短暂改成掉上限，同日用户
            // 判定上限损失会造成不可逆的慢性死亡，已退回扣血，见类注释）。
            // 埋伏基础为7（9）；吞牌成长只属于霜牙自身，十面埋伏收集时不复制成长值。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                // Unblockable：扣血是"代价"不是"受击"，格挡挡不掉（本体 Bloodletting / Offering 同款写法）。
                await CreatureCmdCompat.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, null, this, cardPlay);

                var ambushDamage = (DamageVar)DynamicVars["AmbushDamage"];
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await DamageCmd.Attack(ambushDamage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                        .WithHitFx("vfx/vfx_attack_slash")
                        .Execute(choiceContext);
                }
            }

            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        protected override void AfterDowngraded()
        {
            base.AfterDowngraded();
            DynamicVars.Damage.BaseValue += ExtraDamage;
            ((DamageVar)DynamicVars["AmbushDamage"]).BaseValue += ExtraDamage;
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m);          // 5 → 7
            DynamicVars["AmbushDamage"].UpgradeValueBy(2m); // 7 → 9
        }
    }
}
