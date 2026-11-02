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
    /// <summary>2费：10（14）伤害，同名成长4（5）；埋伏额外执行一次。</summary>
    public sealed class WhetClaw : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(10m, ValueProp.Move),
            new PowerVar<WhetClawPower>(4m),
        ];

        public WhetClaw()
            : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 埋伏 = 额外打出一次：整段效果连做两遍（第二遍会吃到第一遍刚加的那份加值，与"真打出两次"一致）。
            bool ambush = AmbushZone.IsVeteranAmbush(this);
            if (ambush) AmbushEffects.Record(this);
            int times = ambush ? 2 : 1;
            for (int i = 0; i < times; i++)
            {
                // 先打伤害（只吃"之前积累的"加值），再加值
                decimal accumulated = Owner.Creature.GetPower<WhetClawPower>()?.Amount ?? 0m;
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue + accumulated).FromCardCompat(this, cardPlay)
                    .Targeting(cardPlay.Target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                await PowerCmd.Apply<WhetClawPower>(choiceContext, Owner.Creature,
                    DynamicVars["WhetClawPower"].BaseValue, Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(4m);            // 10 → 14
            DynamicVars["WhetClawPower"].UpgradeValueBy(1m);  // +4 → +5
        }
    }
}