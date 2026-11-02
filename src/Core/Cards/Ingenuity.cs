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
    /// 巧思：0费普通技能，获得4（6）点格挡；埋伏额外获得4点格挡。
    /// 前移部分埋伏收益，基础与一次埋伏合计仍为8（10）；十面埋伏只记录额外部分。
    /// </summary>
    public sealed class Ingenuity : CardModel, IAmbushBonusCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(4m, ValueProp.Move),
            new BlockVar("AmbushBlock", 4m, ValueProp.Move),
        ];

        public Ingenuity()
            : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

            // 埋伏段：额外格挡，按「伏兵」层数重复触发（与其他卡的埋伏分支同一套计数）。
            // 2026-09-23 起判定用 IsVeteranAmbush：必须**跨回合**潜伏过，当回合埋了马上带出不给收益。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                AmbushEffects.Record(this);
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["AmbushBlock"], cardPlay);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(2m); // 4 → 6，埋伏额外格挡保持4
        }
    }
}
