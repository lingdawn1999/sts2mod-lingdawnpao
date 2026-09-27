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
    /// 永恒狩猎：3 费金卡能力（引擎档）。**每回合你的入区额度 +1 张（升级再 +1，额度 1→2→3）**。
    /// 定位：伏击线的"供弹"引擎——遗物只保底每回合改道 1 张，本卡把额度往上叠，
    /// 多打出的牌也能进区域喂存量类收益（结算统一在 <see cref="AmbushZone.TryDivertAfterPlayAsync" />）。
    /// 升级只加每回合张数（1→2），不改费用（对标本体同类引擎的升级方式）。
    /// </summary>
    public sealed class EternalHunt : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<EternalHuntPower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<EternalHuntPower>(1m),
        ];

        public EternalHunt()
            : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<EternalHuntPower>(choiceContext, Owner.Creature,
                DynamicVars["EternalHuntPower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars["EternalHuntPower"].UpgradeValueBy(1m); // 1 张 → 2 张
        }
    }
}