using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>2费成长能力；每回合首次带出的牌结算完毕后，获得1（2）点力量与敏捷。</summary>
public sealed class NightPatrol : CardModel, ILingdawnpaoCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<NightPatrolPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<NightPatrolPower>(1m)];
    public NightPatrol() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
        await PowerCmd.Apply<NightPatrolPower>(choiceContext, Owner.Creature,
            DynamicVars["NightPatrolPower"].BaseValue, Owner.Creature, this);
        Owner.GetRelic<FrostFang>()?.Flash();
    }

    protected override void OnUpgrade() => DynamicVars["NightPatrolPower"].UpgradeValueBy(1m);
}
