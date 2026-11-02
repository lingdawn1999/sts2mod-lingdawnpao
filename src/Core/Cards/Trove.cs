using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 珍藏：**0 费金卡技能，自带[gold]虚无[/gold]**（2026-09-28 用户定稿四改）。
    /// 从[gold]抽牌堆[/gold]中选 1 张牌：**直接埋入你的[gold]伏击区域[/gold]**（区域满/冷却回落为加入手牌）。
    /// **每打出一次此牌，本战斗内其费用 +1**（本体 `FranticEscape`/`Modded` 同款 `EnergyCost.AddThisCombat(1)`，
    /// EndOfCombat 到期，跨战斗自动归零）。
    /// **升级：去掉[gold]虚无[/gold]**（走 `RemoveKeyword`，读档时本体重放升级会自动重新移除，存档安全）。
    /// <para>
    /// **设计逻辑**：虚无取代消耗当限制器——攥在手里不打出就回合结束烧掉，逼你当回合用；
    /// 费用递增取代消耗当定价器——重复检索越用越贵。两者都要成立，[gold]消耗[/gold]必须移除
    /// （有消耗的话打一次就进消耗堆，加费是死代码）。
    /// </para>
    /// <para>
    /// **沿革**：①2026-09-21 定稿抽牌堆定向检索；②2026-09-24 加"埋伏牌直接入区"分支；
    /// ③2026-09-25 三改：去虚无、升级改降费、不限牌型；
    /// ④**2026-09-28 四改（本次）**：1 费 → **0 费**，消耗 → **虚无**，升级从"降费"改为"**去虚无**"，
    /// 新增"每打出一次费用+1"。检索核心（抽牌堆选 1 张直接入区）不变。
    /// </para>
    /// <para>
    /// **与「翻雪」的分工**：翻雪（白卡）走**弃牌堆**回收 + 给格挡；本卡走**抽牌堆**定向检索。
    /// **结算顺序（v2）**：检索到的牌直接入区——v2 普通出牌不再自动带出，埋进去就老实待着。
    /// </para>
    /// </summary>
    public sealed class Trove : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

        public Trove()
            : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 每打出一次，本战斗费用 +1（EndOfCombat 到期；先记账后结算，本次已按旧价付费）
            EnergyCost.AddThisCombat(1);

            CardModel? picked = (await CardSelectCmd.FromCombatPile(
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                context: choiceContext,
                pile: PileType.Draw.GetPile(Owner),
                player: Owner)).FirstOrDefault();

            if (picked == null)
            {
                return;
            }

            // 先带出区域旧牌，再入区
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            if (!AmbushZone.CanContinueCombat(Owner)) return;
            AmbushZoneState zone = AmbushZone.For(Owner);
            if (!zone.IsFull && !zone.IsCoolingDown)
            {
                // 从抽牌堆取出后才能入区（同一张牌不能同时属于两个堆，AddInternal 会抛）
                picked.RemoveFromCurrentPile();
                await AmbushZone.BuryGeneratedAsync(choiceContext, Owner, picked, "珍藏");
                return;
            }

            await CardPileCmd.Add(picked, PileType.Hand);
        }

        protected override void OnUpgrade()
        {
            // 质变升级：去掉【虚无】（不再"攥着就烧"，可以等区域空出来）
            RemoveKeyword(CardKeyword.Ethereal);
        }
    }
}
