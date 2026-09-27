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
    /// 珍藏：1 费金卡技能，**抽牌堆定向检索**（2026-09-21 用户定稿；
    /// **2026-09-22 用户要求去掉"检索到的牌本回合免单"**）。
    /// 从[gold]抽牌堆[/gold]中选 1 张牌：**直接埋入你的[gold]伏击区域[/gold]**。
    /// 自带[gold]消耗[/gold]；**升级：降费 1 → 0**。
    /// <para>
    /// **本体对照「秘密武器」`SecretWeapon`**（0 费 · 技能 · **金** · 自身：从抽牌堆选 1 张**攻击牌**
    /// 加入手牌 + 消耗，升级去消耗，见 sts2mod-unciphered/src/Core/Models/Cards/SecretWeapon.cs）。
    /// 我们与它的差异：**贵 1 费、但不限牌型、且能把牌直接送上伏击线**——这是用户拍板的"金卡档通用运转件"。
    /// </para>
    /// <para>
    /// **2026-09-24 用户加"埋伏牌直接入区"分支**：检索到[gold]偷袭[/gold][gold]断腕[/gold]这类埋伏牌时，
    /// **省掉"摸到手 → 花一回合埋进去"** 这两步，直接把它放到伏击线上
    /// （当回合埋入，所以按既有规则只吃基础效果，得等下回合带出才是埋伏收益）。
    /// 埋入走 `AmbushZone.BuryGeneratedAsync`（**不扣费**：这张牌是从抽牌堆白拿的，不是从手牌埋入）。
    /// **区域已满 / 冷却中时回落为"加入手牌"**（与手牌埋入的失败口径一致，不做额外保护）。
    /// </para>
    /// <para>
    /// **2026-09-25 用户三改（本次）**：
    /// ① **去掉[gold]虚无[/gold]**——它当初加虚无是为了和本体许愿拉开辨识度，
    /// 但"不打出就烧掉"和"检索一张立刻入区"的定位互相打架（攥不住就没法等区域空出来），故去掉；
    /// ② **升级位从"去掉消耗"改成"降费 1 → 0"**（消耗改为常驻）——升级买润滑而不是解禁；
    /// ③ **不再限制牌型**：原来只有带[gold]埋伏[/gold]词条的牌才入区，现在**任意牌都能进伏击区**
    /// （埋伏收益仍按原规则只认跨回合带出的埋伏牌，所以非埋伏牌入区买到的是"延迟免费打出 + 占格给增益"）。
    /// 判定原本用标记接口 <see cref="IAmbushBonusCard" />，现在这条分支已不需要它。
    /// </para>
    /// <para>
    /// **结算顺序（v2）**：检索到的牌直接入区——v2 普通出牌不再自动带出，埋进去就老实待着
    /// （区域冷却分支随 v2 语义一并移除：失效只抑制力敏，入区照常）。
    /// </para>
    /// <para>
    /// **与「翻雪」的分工**：翻雪（白卡）走**弃牌堆**回收 + 给格挡；本卡走**抽牌堆**定向检索，
    /// 两张一起补齐"牌堆定向操作"这条通用线（mod 此前只有弃牌堆一侧）。
    /// </para>
    /// </summary>
    public sealed class Trove : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public Trove()
            : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            CardModel? picked = (await CardSelectCmd.FromCombatPile(
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                context: choiceContext,
                pile: PileType.Draw.GetPile(Owner),
                player: Owner)).FirstOrDefault();

            if (picked == null)
            {
                return;
            }

            // 2026-09-25：不再按牌型分流，**任意牌**都优先入区
            AmbushZoneState zone = AmbushZone.For(Owner);
            if (!zone.IsFull)
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
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（消耗现在是常驻，不再随升级去掉）
        }
    }
}