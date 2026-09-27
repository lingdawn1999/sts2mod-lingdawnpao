using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 贪吃的旱獭：**动物**衍生牌（Token，**垃圾档**），0 费技能，自带[gold]消耗[/gold]。
    /// 回复 **1** 点生命（升级 **2**）。
    /// **为什么算垃圾**：战斗内的回血几乎没有构筑价值（不像格挡能挡伤害、也不像过牌能推进），
    /// 抽到它基本等于"白来一张、花 0 费清掉"——刻意用它把「动物朋友」的每回合期望值拉低，
    /// 让这张金卡是"博运气"而不是"稳定收益"（用户 2026-09-21："放点垃圾填一下"）。
    /// **2026-09-24 用户削弱 2/4 → 1/2**（改因：战斗内回血被判定偏强——「呼吸」那条无限刷血的
    /// 路子刚被「永恒呼吸」加[gold]消耗[/gold]掐掉，同类"场内回血"一并收口；
    /// 顺带让它更贴合"垃圾档"的定位）。
    /// </summary>
    public sealed class GreedyMarmot : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/greedy_marmot.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new HealVar(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public GreedyMarmot()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Heal.UpgradeValueBy(1m);   // 1 → 2
        }
    }
}