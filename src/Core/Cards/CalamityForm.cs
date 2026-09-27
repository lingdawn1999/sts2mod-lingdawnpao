using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狩猎形态：3 费**先古（Ancient）**能力（形态牌），自带**保留**。升级只减 1 费（3→2，对标本体 Tracking）。
    /// 2026-09-21 由金卡升为先古（用户："通用的终端先古"）——先古卡不进普通奖励，唯一途径是奥罗巴斯事件
    /// 的「尘封魔典」（抽到即自动升级，玩家实际见到的永远是 2 费版；转换表先古卡被它排除，不会挤占）。
    /// 3 费带保留、升级减到 2 费均为用户指定。
    /// 效果：① 你的攻击无视格挡；② 在你的回合开始时，给予所有敌人 1 层「破绽」
    /// （受到的**所有**伤害 +34%、持续 1 回合）。
    /// 加伤以"每回合刷新的敌方 debuff"形式给出（用户定案）：人工制品会先挡掉这次施加，
    /// 所以遇到人工制品敌人要先用其它手段削层才能吃到加成。
    /// 形态的收益从下个回合开始（同本体 DemonForm），打出当回合只有"无视格挡"在生效。
    /// </summary>
    public sealed class CalamityForm : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromKeyword(CardKeyword.Retain),
            HoverTipFactory.FromPower<CalamityMarkPower>(),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

        public CalamityForm()
            : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<CalamityFormPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 3 → 2
        }
    }
}