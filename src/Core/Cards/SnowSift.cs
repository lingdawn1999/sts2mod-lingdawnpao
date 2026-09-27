using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 翻雪：1 费普通（白）技能，**防御型回收**（2026-09-21 用户定稿）。
    /// 获得 3 点格挡；从**弃牌堆**选 1 张牌加入手牌。自带[gold]消耗[/gold]。
    /// **升级**：格挡 3 → 5，并**去掉消耗**。
    /// **完整照抄本体「全息影像」Hologram**（1 费 · 技能 · **白** · 自身：3 格挡 + 从弃牌堆选 1 张 + 消耗；
    /// 升级 5 格挡 + 去消耗，见 sts2mod-unciphered/src/Core/Models/Cards/Hologram.cs）。
    /// **为什么放"防御侧"**：2026-09-21 攻防浓度体检显示 mod **攻击 29.5% 已与同源的毒猎人 Silent（30.7%）齐平**，
    /// 但**提供格挡只有 14.1%、低于 Silent 的 18.2% 与本体平均 18.3%**——格挡是唯一明显偏低的项，
    /// 所以这张走 3 格挡的低价 + 高精准回收（"回收"比"抽牌"精准，故格挡只给本体的 3 点定价；
    /// 与另一张白卡「雪绒护体」（1 费 7 格挡 + 抽 1）错开：低格挡 / 高精准 vs 高格挡 / 随机）。
    /// **mod 首张"涉弃牌堆"的牌**（体检时 `PileType.Discard` 零匹配）。
    /// </summary>
    public sealed class SnowSift : CardModel, ILingdawnpaoCard
    {
        public override bool GainsBlock => true;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.Static(StaticHoverTip.Block),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new BlockVar(3m, ValueProp.Move),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public SnowSift()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

            CardModel? picked = (await CardSelectCmd.FromCombatPile(
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                context: choiceContext,
                pile: PileType.Discard.GetPile(Owner),
                player: Owner)).FirstOrDefault();

            if (picked != null)
            {
                await CardPileCmd.Add(picked, PileType.Hand);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(2m);      // 3 → 5
            RemoveKeyword(CardKeyword.Exhaust);        // 升级去掉消耗（照本体 Hologram）
        }
    }
}