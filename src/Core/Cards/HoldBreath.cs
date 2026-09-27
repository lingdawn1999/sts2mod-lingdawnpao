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
    /// 屏息：1 费罕见技能，获得 7 点格挡（升级 10）。
    /// 埋伏（**跨回合**从伏击区域带出打出）：额外获得 1 点能量与 3 点格挡——埋的时候已经付过这 1 费，
    /// 带出来等于把费用收回来，与倾泄/猛虎连击一次带出多张时组成爆发回合。
    /// **2026-09-23 埋伏体系大改**：判定从"被带出"改为"跨回合潜伏过"
    /// （见 <see cref="AmbushZone.IsVeteranAmbush" />），埋伏段在"回 1 能量"之外**再加 3 点格挡**
    /// （`AmbushBlock`，不随升级成长）——换取真等一回合 + 占格的代价。
    /// 费用刻意定 1 而非 0：埋牌当场付本牌费用，0 费埋牌等于免费，会退化成免费能量引擎。
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
            new BlockVar(7m, ValueProp.Move),
            new BlockVar("AmbushBlock", 3m, ValueProp.Move),
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
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["AmbushBlock"], cardPlay);
                    await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(3m); // 7 → 10（埋伏段 3 不随升级成长）
        }
    }
}