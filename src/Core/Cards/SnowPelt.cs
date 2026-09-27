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
    /// 雪绒护体：1 费普通技能（2026-09-27 用户定稿重做，顶白卡 Backflip 位）。
    /// 获得 4 点[gold]格挡[/gold]，**抽 2 张牌**。升级：格挡 4 → 6（抽牌不变）。
    /// <para>
    /// **沿革**：初版是本体 `ShrugItOff` 同款（8 挡抽 1 → 升 12 挡），2026-09-23 定案 8/12。
    /// 2026-09-27 过牌结构对账发现：本体 Silent 白卡有 **Backflip（1 费无条件抽 2）**，
    /// 我们白卡只能抽 1（无条件抽 2 缺位），于是本卡重做——格挡降为 4 点的"副业"，
    /// 主业变成**抽 2**：从"高格挡带过牌"转成"过牌为主、顺手挡一下"，
    /// 与「探爪」（4伤4挡抽1）构成白卡双 cantrip，格挡浓度由纯防御件（厚绒/守住等）保底。
    /// 与「暖雪」（0 费抽 2 消耗）不撞：一个是弹药、一个是循环引擎；
    /// 与「雪原疾行」（1 费抽 2+埋，蓝）错开：无埋入、有格挡、稀有度差一档。
    /// </para>
    /// </summary>
    public sealed class SnowPelt : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(4m, ValueProp.Move),
            new CardsVar(2),
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
            DynamicVars.Block.UpgradeValueBy(2m);   // 4 → 6（抽牌不变）
        }
    }
}