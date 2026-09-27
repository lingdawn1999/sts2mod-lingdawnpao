using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 扑击：1 费普通攻击，造成 3 点伤害（升级 6），并把**抽牌堆顶**的牌放进[gold]伏击区域[/gold]。
    /// 定位：伏击线的"铺一格 + 顺带输出"件——比「设伏」（1 费埋 2 张生成的暗袭）少一张，
    /// 但埋的是**你自己牌组里的真牌**（质量更高，且顺手压缩牌组），并自带 3 点伤害不空放。
    /// **区满 / 抽牌堆空时埋不进**：此时它就是一张 3 点伤害的小攻击（照「设伏」「捕猎」的既有约定，
    /// 不做额外保护、也不阻止打出）。
    /// **结算顺序（v2）**：伤害结算完直接埋抽牌堆顶——v2 普通出牌不再自动带出，埋进去就老实待着。
    /// <para>
    /// **设计沿革（2026-09-23，同一张卡改了四轮，记全以免再走回头路）**：
    /// ① 初版＝本体「全身撞击」BodySlam（伤害 = 当前格挡）——用户判定成废卡，理由是本 mod
    /// **没有格挡保留**（本体 `Barricade`）也**没有格挡翻倍**（本体 `Entrench`），
    /// 可靠防御集中在 1 费 5~7 点，3 费回合最多攒 10~15 格挡 → 等于"花 2~3 费凑两张牌换 10~15 伤"，
    /// **它缺的不是数值而是上限**。
    /// ② 改成"本回合每当你造成攻击伤害就获得格挡"——**与本体战士的「狂怒」Rage 撞车**，用户撤销。
    /// ③ 改成"获得 3 格挡 + 埋抽牌堆顶"——用户改口：带"击"字就还是该打伤害、不要防。
    /// ④ **最终定稿＝本版**：造成 3 点伤害 + 埋抽牌堆顶。升级从原来的"费用 1 → 0"改成"**伤害 3 → 6**"
    /// —— 0 费埋牌等于每回合白送一格（区域力敏会滚起来），这个口径早前在「屏息」上已经判过，
    /// 所以升级改为买伤害而不是买费用。
    /// </para>
    /// <para>
    /// ⚠️ 与埋伏大改的叠加：埋伏现在是"跨回合才算"（`AmbushZone.IsVeteranAmbush`），
    /// 所以扑击埋进去的牌**当回合带出来不给埋伏收益**——本牌是"为下回合铺垫"的引擎件，
    /// 不是当回合兑现件。它真正喂的是区域力敏、以及「收网」「霜牙」「倾泄」这类按区域存量的牌。
    /// </para>
    /// </summary>
    public sealed class BodyPounce : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(3m, ValueProp.Move),
        ];

        public BodyPounce()
            : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);

            // 先带出区域旧牌，再生成新牌
            await AmbushZone.BringOutBeforeGenerateAsync(choiceContext, cardPlay.Target, Owner);
            // 埋抽牌堆顶。区满 / 抽牌堆空时 BuryTopOfDrawPileAsync 返回 false，本牌就只是一张 3 伤攻击。
            await AmbushZone.BuryTopOfDrawPileAsync(choiceContext, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(3m); // 3 → 6（升级买伤害，不再降费）
        }
    }
}
