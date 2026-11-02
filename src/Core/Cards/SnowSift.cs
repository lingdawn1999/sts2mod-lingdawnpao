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
    /// 翻雪：1 费普通（白）技能，**防御型回收 → 伏击线喂牌**（2026-09-27 用户定稿）。
    /// 获得 3 点格挡；从**弃牌堆**选 1 张牌加入[gold]伏击区域[/gold]。自带[gold]消耗[/gold]。
    /// **升级**：格挡 3 → 5，并**去掉消耗**。
    /// <para>
    /// **沿革**：初版整张照抄本体「全息影像」Hologram（3 格挡 + 弃牌堆选 1 张**回手牌** + 消耗；
    /// 升级 5 格挡 + 去消耗）——2026-09-21 定稿时是"防御侧低价 + 高精准回收"。
    /// **2026-09-27 用户重做**：回收目的地从手牌改成[gold]伏击区域[/gold]——同一笔精准回收，
    /// 从"补手牌"变成"给区域递弹药"（区域力敏 / 按区域存量的牌都吃它喂），格挡与升级数值不变。
    /// 本卡仍是 mod 首张"涉弃牌堆"的牌。
    /// </para>
    /// <para>
    /// **埋入路径**：<see cref="AmbushZone.BuryFromDiscardAsync" />（「循迹」同款：要求牌真的在弃牌堆，
    /// 内部做 RemoveFromCurrentPile + 完整入区记账；区域已满 / 冷却时埋不进，选中的牌留在弃牌堆，
    /// 照「设伏」既有约定不满足即空放）。弃牌堆的牌没有桌面节点，无飞行动画。
    /// </para>
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
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 先用当前敏捷结算本牌格挡；仍在回收入区前带出旧牌，避免立即带出新牌。
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);

            CardModel? picked = (await CardSelectCmd.FromCombatPile(
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                context: choiceContext,
                pile: PileType.Discard.GetPile(Owner),
                player: Owner)).FirstOrDefault();

            if (picked != null)
            {
                await AmbushZone.BuryFromDiscardAsync(choiceContext, Owner, picked);
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Block.UpgradeValueBy(2m);      // 3 → 5
            RemoveKeyword(CardKeyword.Exhaust);        // 升级去掉消耗（照本体 Hologram）
        }
    }
}
