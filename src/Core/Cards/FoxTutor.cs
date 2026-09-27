using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 好学的狐狸哥哥：**动物**衍生牌（Token），0 费技能，自带[gold]消耗[/gold]。
    /// 将 1 张[gold]猛狐连击[/gold]加入你的手牌；**升级后给的是升级版**（猛狐连击升级 = 1 费 → 0 费）。
    /// 生成写法照 mod 内「雪球」的 `CreateIntoHandAsync`：`CreateCard` → `CardCmd.Upgrade`（升级时）→ 加入手牌。
    /// <para>
    /// ⚠️ 卡面文案写成「获得[gold]猛虎连击[/gold]？？」（升级显示「猛虎连击+？？」）是**用户特意要求的玩笑**
    /// —— 名字故意写错 + 多余问号，**不是笔误，别顺手"修正"成「猛狐连击」**；悬停提示里能看到真身。
    /// </para>
    /// </summary>
    public sealed class FoxTutor : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/fox_tutor.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
            HoverTipFactory.FromCard<FierceFoxCombo>(upgrade: IsUpgraded),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public FoxTutor()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            FierceFoxCombo combo = CombatState!.CreateCard<FierceFoxCombo>(Owner);
            if (IsUpgraded)
            {
                CardCmd.Upgrade(combo);
            }

            await CardPileCmd.AddGeneratedCardsToCombat([combo], PileType.Hand, Owner);
        }
    }
}