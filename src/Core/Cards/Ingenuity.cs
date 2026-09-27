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
    /// 巧思：1 费普通技能，获得 5 点格挡（升级 7）并抽 1 张牌；作为「埋伏」带出打出时
    /// 额外获得 5 点格挡（受「伏兵」额外触发次数加成）。
    /// 定位：伏击流的循环件——正常打是"1 费 5 格挡抽 1"（对标本体剑柄打击 9 伤抽 1），
    /// 跨回合埋伏过再带出则白赚 5 格挡，与倾泄/猛虎连击的多段带出配合时能持续续上手牌。
    /// **沿革**：初版为 0 费 4 格挡、仅埋伏段抽 1；2026-09-23 用户先要求改 1 费，
    /// 随后判定 1 费 4 格挡太亏模（剑柄打击 1 费 9 伤抽 1），改为 1 费 5 格挡 + 无条件抽 1、
    /// 埋伏段从"抽 1"改为"额外格挡"。**同日埋伏体系大改**（跨回合才算埋伏，见
    /// <see cref="AmbushZone.IsVeteranAmbush" />）后，埋伏段从 +3 格挡上调为 **+5 格挡**：
    /// 代价从"当回合切形态白嫖"变成"真等一回合 + 占格"，收益随之抬高。
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
            new BlockVar(5m, ValueProp.Move),
            new BlockVar("AmbushBlock", 5m, ValueProp.Move),
        ];

        public Ingenuity()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            // 抽牌改为无条件（不再是埋伏专属）：对标剑柄打击，1 费本体就值 5 格挡 + 抽 1
            await CardPileCmd.Draw(choiceContext, 1m, Owner);

            // 埋伏段：额外格挡，按「伏兵」层数重复触发（与其他卡的埋伏分支同一套计数）。
            // 2026-09-23 起判定用 IsVeteranAmbush：必须**跨回合**潜伏过，当回合埋了马上带出不给收益。
            if (AmbushZone.IsVeteranAmbush(this))
            {
                for (int i = 0; i < AmbushZone.AmbushTriggerCount(Owner); i++)
                {
                    await CreatureCmd.GainBlock(Owner.Creature, (BlockVar)DynamicVars["AmbushBlock"], cardPlay);
                }
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(2m); // 5 → 7（埋伏段 3 不随升级成长）
        }
    }
}
