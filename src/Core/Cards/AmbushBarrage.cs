using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core;

/// <summary>Persistent, per-player collection of distinct Ambush bonuses. Replays bonuses, never source cards.</summary>
public sealed class AmbushBarrage : CardModel, IAmbushBonusCard
{
    private int _appliedCostGrowth;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(5m),
        new ExtraDamageVar(4m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) => AmbushZone.For(card.Owner).Effects.Count / 2),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return new HoverTip(new LocString("card_keywords", "AMBUSH.title"),
                new LocString("card_keywords", "AMBUSH.description"));
            if (!IsMutable || Owner?.PlayerCombatState == null) yield break;
            RecordedAmbush[] effects = AmbushZone.For(Owner).Effects.Snapshot();
            var title = new LocString("cards", "AMBUSH_BARRAGE.collectedTitle");
            title.Add("Count", effects.Length);
            string summary = effects.Length == 0
                ? new LocString("cards", "AMBUSH_BARRAGE.collectedEmpty").GetFormattedText()
                : string.Join("\n", effects.Select(AmbushEffects.Describe));
            yield return new HoverTip(title, summary);
            if (effects.Any(effect => (effect.Kind is AmbushEffectKind.SneakAttack or AmbushEffectKind.DamBreak) && effect.Secondary > 0m))
                yield return HoverTipFactory.FromPower<WeakPower>();
            if (effects.Any(effect => effect.Kind == AmbushEffectKind.ThroatGrip && effect.Secondary > 0m))
                yield return HoverTipFactory.FromPower<VulnerablePower>();
        }
    }

    public AmbushBarrage() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Snapshot before resolving: replays cannot collect themselves or change this play's contents.
        RecordedAmbush[] effects = AmbushZone.IsVeteranAmbush(this)
            ? AmbushZone.For(Owner).Effects.Snapshot() : [];
        bool allEnemies = effects.Any(effect => effect.Kind == AmbushEffectKind.AmbushPounce);
        decimal bonus = effects.Where(effect => effect.Kind == AmbushEffectKind.AmbushPounce).Sum(effect => effect.Amount);
        decimal damage = DynamicVars.CalculatedDamage.Calculate(cardPlay.Target) + bonus;
        int triggers = AmbushZone.AmbushTriggerCount(Owner);
        await AmbushEffectLedger.ResolveAsync(effects, triggers,
            () => AttackAsync(choiceContext, cardPlay, damage, allEnemies),
            effect => AmbushEffects.ResolveAsync(effect, this, choiceContext, cardPlay, allEnemies, bonus),
            () => Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding);
    }

    internal async Task AttackAsync(PlayerChoiceContext context, CardPlay play, decimal damage, bool allEnemies,
        Creature? preferredTarget = null)
    {
        if (allEnemies)
        {
            await DamageCmd.Attack(damage).FromCardCompat(this, play).TargetingAllOpponents(CombatState!)
                .WithHitFx("vfx/vfx_attack_slash").Execute(context);
            return;
        }
        var target = preferredTarget ?? ResolveAttackTarget(play);
        if (target == null || !target.IsAlive) return;
        await DamageCmd.Attack(damage).FromCardCompat(this, play).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }

    internal Creature? ResolveAttackTarget(CardPlay play)
    {
        var target = play.Target;
        if (target == null || !target.IsAlive)
        {
            var enemies = CombatState!.HittableEnemies;
            if (enemies.Count == 0) return null;
            target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        }
        return target;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this) ApplyCostGrowth(AmbushZone.For(Owner).Effects.Count);
        return Task.CompletedTask;
    }

    public static void SyncCosts(Player player)
    {
        if (player.PlayerCombatState == null) return;
        AmbushZoneState zone = AmbushZone.For(player);
        foreach (AmbushBarrage card in player.PlayerCombatState.AllCards.Concat(zone.Cards).OfType<AmbushBarrage>().Distinct().ToArray())
            card.ApplyCostGrowth(zone.Effects.Count);
    }

    private void ApplyCostGrowth(int count)
    {
        int costGrowth = count / 2;
        int delta = costGrowth - _appliedCostGrowth;
        if (delta == 0) return;
        EnergyCost.AddThisCombat(delta);
        _appliedCostGrowth = costGrowth;
        InvokeEnergyCostChanged();
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(3m);
        DynamicVars.ExtraDamage.UpgradeValueBy(2m);
    }
}
