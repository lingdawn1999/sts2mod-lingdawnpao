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
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 猛扑：**2 费普通（白）攻击**，**高伤单体 + 一张[gold]晕眩[/gold]**。
    /// 造成 **23** 点伤害，并将 1 张[gold]晕眩[/gold]洗入你的[gold]抽牌堆[/gold]。**升级：30 点**（+7）。
    /// <para>
    /// **沿革**：初版是"单纯打得疼的白卡"（3 费 25 伤）；**2026-09-24 用户定稿**改为
    /// **2 费 23 伤 + 洗入 1 张晕眩**——同一天用户先提费到 3 费、又判定"纯数值白卡没有辨识度"，
    /// 最终选择"降回 2 费、用一张晕眩当代价换更高的单卡数值"。
    /// 升级仍是 +7（23 → 30），与原本 +8/25 的比例接近，敲与不敲的落点差没变。
    /// </para>
    /// <para>
    /// **为什么要塞晕眩**：mod 的白卡攻击清一色"带功能"（探爪=伤+格挡、雪爪突击=伤+抽 1、扑击=格挡兑现、
    /// 倾巢而出/连环爪=伏击联动），本卡的定位是"低门槛的暴力件"，代价改成**污染自己的抽牌堆**——
    /// 晕眩自带 `CardKeyword.Unplayable`，抽到手里就是一张废牌（回合结束自行消耗，不会像伤口那样永久堵手，见
    /// 本体 `CardModel` 的回合末清理）。这让它区别于"纯白板伤害"：数值给得高，但要拿牌序去换。
    /// </para>
    /// <para>
    /// **本体锚点**：`FuneraryMask`（遗物：第一回合把魂洗进抽牌堆）——洗入走
    /// `CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, Owner, CardPilePosition.Random)`
    /// （**Random = 插进抽牌堆的随机下标，不重排其余牌**），并配 `CardCmd.PreviewCardPileAdd` + 短暂停顿
    /// 让玩家看清牌被洗了进去（同本体 `BoostAway` / `Overclock` 的写法）。
    /// </summary>
    public sealed class FiercePounce : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Dazed>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(23m, ValueProp.Move),
        ];

        public FiercePounce()
            : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            // 代价：往自己的抽牌堆里塞一张晕眩（随机位置）。照本体 FuneraryMask / BoostAway 的写法。
            Dazed dazed = CombatState!.CreateCard<Dazed>(Owner);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(
                dazed, PileType.Draw, Owner, CardPilePosition.Random));
            await Cmd.Wait(0.5f);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(7m);   // 23 → 30
        }
    }
}