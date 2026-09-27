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
    /// 护短的雪豹妈妈：**动物**衍生牌（Token），0 费技能，自带[gold]消耗[/gold]与[gold]保留[/gold]。
    /// 本回合你受到攻击时，对伤害来源返还 **3** 点伤害（升级 **5**）。
    /// **效果窗口**：实现为"**直到你的下个回合开始**"（在敌方回合结束时自解）——因为主要挨打发生在敌方回合，
    /// 若在自己的回合末就解掉，这牌等于废牌；口径与 mod 内的「听桥」、本体「火焰屏障」一致。
    /// **反伤写法定标本体 `ThornsPower`**（[ThornsPower.cs](file:///d:/sts2mod/sts2mod-unciphered/src/Core/Models/Powers/ThornsPower.cs)）：
    /// `BeforeDamageReceived` 里 `target == Owner && dealer != null && props.IsPoweredAttack()` → `CreatureCmd.Damage`；
    /// 伤害用 `ValueProp.Unpowered | ValueProp.SkipHurtAnim`，即**和荆棘一样不吃[gold]力量[/gold]**（听桥那条走 Move 吃力量，
    /// 是用户当时单独指定的，本卡按荆棘口径）。层数走 Counter：**打两张就是 6 点**。
    /// ⚠️ 不复用本体 `ThornsPower` 的原因：它是永久的，若靠"回合末移除"来收窗口，会连带把「棘背龙形态」给的荆棘一起清掉。
    /// </summary>
    public sealed class DotingMom : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/doting_mom.tres";
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "ANIMAL.title"), new LocString("card_keywords", "ANIMAL.description")),
            HoverTipFactory.FromPower<DotingMomPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<DotingMomPower>(3m),
        ];

        // 保留：用户 2026-09-21 指定加的。它是 0 费 token，留到"预计要挨打的那一回合"再打才有价值，
        // 没有保留时抽到的当回合用不上就只能烂在手里（或被迫带消耗白交）。
        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

        public DotingMom()
            : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await PowerCmd.Apply<DotingMomPower>(choiceContext, Owner.Creature,
                DynamicVars["DotingMomPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["DotingMomPower"].UpgradeValueBy(2m);   // 3 → 5
        }
    }
}