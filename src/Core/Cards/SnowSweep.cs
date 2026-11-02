using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core;

/// <summary>拨雪：1费稀有技能，消耗、固有。给予所有敌人2（3）层虚弱，抽2（3）张牌；升级虚弱与抽牌各+1。</summary>
public sealed class SnowSweep : CardModel, ILingdawnpaoCard
{
    // 固有（2026-10-28 用户定稿：基础就带，开局必抽早挂虚弱；升级位只买数值，不再给固有）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Innate];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<WeakPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<WeakPower>(2m), new CardsVar(2)];

    public SnowSweep() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!AmbushZone.CanContinueCombat(Owner)) return;
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        foreach (var enemy in CombatState!.HittableEnemies.ToArray())
        {
            if (!AmbushZone.CanContinueCombat(Owner)) break;
            await PowerCmd.Apply<WeakPower>(choiceContext, enemy,
                DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);
        }
        if (AmbushZone.CanContinueCombat(Owner))
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["WeakPower"].UpgradeValueBy(1m); // 虚弱 2 → 3
        DynamicVars.Cards.UpgradeValueBy(1m);        // 抽牌 2 → 3
    }
}
