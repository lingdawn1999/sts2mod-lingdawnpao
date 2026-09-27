using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪绒护体：1 费普通技能（通用牌）。
    /// 获得 8 点格挡，抽 1 张牌。升级：格挡 8 → 12。
    /// 定位：防御 + 运转二合一。**本体同型是 `ShrugItOff`（1 费白卡：8 格挡 + 抽 1）——本卡与它完全同档。**
    /// **沿革**：初版定 6/9，用户判定"数值低了"加强到 **7/11**；
    /// 2026-09-23 数值体检按本体同型判"比 ShrugItOff 少 1 点格挡"（旧注释里"本体 1 费防御的白卡数值约 5"
    /// 拿错了参照物——那是纯格挡件「绒盾」的档位，带抽牌的防御件要另算），用户定案 **8/12**。
    /// </summary>
    public sealed class SnowPelt : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(8m, ValueProp.Move),
            new CardsVar(1),
        ];

        public SnowPelt()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(4m);   // 8 → 12
        }
    }
}