using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 陷阱回收：1 费罕见（蓝）技能。
    /// 把伏击区域里的牌全部收回手牌，并获得**这些牌费用之和**的能量（**无额外加值**）。
    /// 与「重整计划」的分工：重整计划把区域内容烧掉、换成固定的手牌与能量；
    /// 本卡把牌**原样收回**——区域变空（随区域来的那部分 +力量/+敏捷也随之消失），
    /// 但牌本身没损失，可以重新埋一次。定位是"拆掉陷阱、收回本钱"，**不是能量引擎**：
    /// 返还的是当初埋它们花掉的钱，自己还倒贴 1 费（升级后倒贴 0 费）。
    /// 费用按卡面名义值统计（`CostModifiers.None`，不跑战斗内 hook），X 费牌记 0。
    /// <para>
    /// **升级沿革**：原为"加[gold]保留[/gold]"→ 2026-09-24 改为"额外返还 1 点能量"
    /// → 2026-09-25 那 1 点**下放到基础**（基础即"能量之和 +1"）、升级改为"空区域也保底"
    /// → **2026-09-25 再调整（用户）**：基础 **0 费 → 1 费**、**去掉自带[gold]消耗[/gold]**、
    /// 升级位换成**降费 1 → 0**（保底词条删除）
    /// → **同日第三次（用户）**：基础那个 **+1 也去掉**，只返还"费用之和"，
    /// 让它彻底变成"回手配件"、不再往上刷资源。
    /// </para>
    /// </summary>
    public sealed class TrapRecovery : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            EnergyHoverTip,
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        public TrapRecovery()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 先把费用之和算出来：牌收回手牌后就查不到"区域里的它们"了
            int refund = AmbushZone.For(Owner).Pile.Cards
                .Sum(card => card.EnergyCost.CostsX
                    ? 0
                    : Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.None)));

            int returned = await AmbushZone.ReturnAllToHandAsync(choiceContext, Owner);
            // 只返还"费用之和"，没有加值；区域空（或全是 X 费/0 费牌）时无事可结算
            if (refund > 0)
            {
                Log.Info($"陷阱回收：收回 {returned} 张，返还 {refund} 点能量");
                await PlayerCmd.GainEnergy(refund, Owner);
            }

            // 区域清空后统一重算增益与层数（ReturnAllToHandAsync 刻意不自己重算）
            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（升级位只买费用，无变量可加）
        }
    }
}