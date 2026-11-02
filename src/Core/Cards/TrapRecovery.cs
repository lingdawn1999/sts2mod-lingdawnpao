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
    /// <summary>陷阱回收：1（0）费罕见技能，不带保留。将区域牌收回手牌，并获得其名义耗能之和的能量。</summary>
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