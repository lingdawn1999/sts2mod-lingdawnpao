using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜天：2 费金卡能力。**在你的回合开始时，给予所有敌人 5 层冰冻**（升级 7），可叠加。
    /// 冰冻线的"自动引擎"：每回合免费推进全场层数，喂「雪崩」（每层换伤害）、
    /// 「深冬」（已有冰冻则加量）、「冰壳」（自身层数转格挡）。
    /// <para>
    /// ⚠️ **不要给它降费/固有**（2026-10-28 实测复盘：1 费+固有=开局即滚的全自动永动机，
    /// 强度直接失控）。它是引擎牌，2 费的"延迟+开销"就是它的定价。
    /// </para>
    /// </summary>
    public sealed class IceAge : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<IceAgePower>(5m),
        ];

        public IceAge()
            : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<IceAgePower>(choiceContext, Owner.Creature,
                DynamicVars["IceAgePower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars["IceAgePower"].UpgradeValueBy(2m); // 5 层 → 7 层
        }
    }
}