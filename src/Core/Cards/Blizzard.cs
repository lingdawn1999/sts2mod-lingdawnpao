using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>基础全体8（10）冰冻；埋伏另行施加3（4）冰冻，伏兵只重复额外部分。</summary>
    public sealed class Blizzard : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(8m),
            new PowerVar<FrostbitePower>("AmbushBonus", 3m),
        ];

        public Blizzard()
            : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await FrostbitePower.ApplyFrostToAllAsync(choiceContext, CombatState!.HittableEnemies.ToList(),
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                    await FrostbitePower.ApplyFrostToAllAsync(choiceContext, CombatState!.HittableEnemies.ToList(),
                        DynamicVars["AmbushBonus"].BaseValue, Owner.Creature, this);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(2m);   // 基础 8 → 10
            DynamicVars["AmbushBonus"].UpgradeValueBy(1m);      // 埋伏 +3 → +4，十面埋伏仅收集额外部分
        }
    }
}
