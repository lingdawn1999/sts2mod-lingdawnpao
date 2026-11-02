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
    /// <summary>
    /// 屏息：1费罕见技能，获得9点格挡；埋伏获得1（2）点能量。
    /// 升级只提高埋伏给费，十面埋伏仅收集给费部分。
    /// </summary>
    public sealed class HoldBreath : CardModel, IAmbushBonusCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            EnergyHoverTip,
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(9m, ValueProp.Move),
            new EnergyVar(1),
        ];

        public HoldBreath()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Energy.UpgradeValueBy(1m); // 1 → 2，基础格挡不变
        }
    }
}
