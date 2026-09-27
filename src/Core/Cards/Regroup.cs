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
    /// 重整计划：1 费罕见（蓝）技能，自带消耗。
    /// 把伏击区域里的牌全部置入消耗牌堆（不触发其埋伏效果、也不带出打出），
    /// **每消耗一张就抽 1 张牌、获得 1 点能量**（升级改为每张抽 2 张牌，能量不升级）。
    /// 定位：兑现器的**资源型**版本——把"埋着但一时带不出来"的牌重新整编成手牌与能量；
    /// 与本流派的另一条兑现路线「倾泄」分工明确：倾泄触发牌本身的效果（牌进弃牌堆），
    /// 本卡只把牌换成资源（牌进消耗牌堆）。伤害型的兑现器留给后续金卡。
    /// **沿革**：初版是"抽 N 张 + 固定 2 点能量（升级 3）"，
    /// 2026-09-23 用户改为**逐张结算**（每张 1 能量、升级每张 2 能量）——
    /// 顺带让卡面文案从三行压到两行。⚠️ 这意味着空区域时**一点能量也没有**
    /// （初版无论区域空不空都白给 2 点），1 张时也比初版少 1 点，2 张持平，3 张起才变强。
    /// <para>
    /// **2026-09-25 用户重定升级位**：升级从"每张 2 点能量"改成"**每张抽 2 张牌**"，
    /// **能量固定每张 1 点**（不再升级）。也就是升级收益从"回费"挪到"补手牌"——
    /// 本卡自带[gold]消耗[/gold]、用完即走，回费再多也只是把当回合的费用补回来（与"清空区域"的
    /// 兑现定位重复），而补手牌能让兑现完的区域立刻重新有料可埋。
    /// </para>
    /// <para>
    /// **2026-09-25 再改（用户："改成升级-费"）**：升级位让给**降费 1 → 0**，
    /// 抽牌与能量都**固定不升级**（每张抽 1 张、回 1 点）。
    /// 本流派的三张兑现器升级位至此统一："买费用/润滑"而不是"买数值"。
    /// </para>
    /// </summary>
    public sealed class Regroup : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            EnergyHoverTip,
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CardsVar(1),
            new EnergyVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public Regroup()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            int exhausted = await AmbushZone.ExhaustAllAsync(choiceContext, Owner);
            if (exhausted > 0)
            {
                // 逐张抽牌：每消耗一张抽 DynamicVars.Cards 张
                await CardPileCmd.Draw(choiceContext, exhausted * DynamicVars.Cards.IntValue, Owner);
                // 逐张回能：每消耗一张回 DynamicVars.Energy 点
                await PlayerCmd.GainEnergy(exhausted * DynamicVars.Energy.IntValue, Owner);
            }
            // 清空后统一重算区域增益与层数（ExhaustAllAsync 刻意不自己重算）
            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（抽牌与能量固定不升级）
        }
    }
}