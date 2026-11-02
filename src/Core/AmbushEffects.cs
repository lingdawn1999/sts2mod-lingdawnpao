using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core;

/// <summary>Records only actual bonus branches, not merely cards brought out of the zone.</summary>
internal static class AmbushEffects
{
    public static void Record(CardModel card)
    {
        if (card is AmbushBarrage || !AmbushZone.IsVeteranAmbush(card)
            || CombatManager.Instance.IsOverOrEnding) return;
        decimal Value(string name) => card.DynamicVars[name].BaseValue;
        RecordedAmbush? effect = card switch
        {
            DamBreak => new(AmbushEffectKind.DamBreak, Value("AmbushDamage"), Value("AmbushWeak")),
            SneakAttack => new(AmbushEffectKind.SneakAttack, Value("AmbushDamage"), Value("AmbushWeak")),
            Ingenuity => new(AmbushEffectKind.Ingenuity, Value("AmbushBlock")),
            ChainClaw => new(AmbushEffectKind.ChainClaw, 1m),
            ClawAssault => new(AmbushEffectKind.ClawAssault, Value("AmbushCards")),
            AmbushPounce => new(AmbushEffectKind.AmbushPounce, Value("AmbushBonus")),
            ThroatGrip => new(AmbushEffectKind.ThroatGrip, Value("AmbushDamage"), Value("AmbushVulnerable")),
            HoldBreath => new(AmbushEffectKind.HoldBreath, 0m, Value("Energy")),
            Hunt => new(AmbushEffectKind.Hunt, 1m),
            WhetClaw => new(AmbushEffectKind.WhetClaw),
            Blizzard => new(AmbushEffectKind.Blizzard, Value("AmbushBonus")),
            DeepBite => new(AmbushEffectKind.DeepBite),
            // Frost Fang's stolen damage belongs to its base growth, never to the collected bonus.
            FrostFangCard => new(AmbushEffectKind.FrostFangCard, card.IsUpgraded ? 9m : 7m, Value("HpLoss")),
            _ => null,
        };
        if (effect == null) return;
        AmbushZone.For(card.Owner).Effects.Record(effect with { Upgraded = card.IsUpgraded });
        AmbushBarrage.SyncCosts(card.Owner);
    }

    public static string Describe(RecordedAmbush effect)
    {
        string id = effect.Kind switch
        {
            AmbushEffectKind.SneakAttack => "SNEAK_ATTACK",
            AmbushEffectKind.DamBreak => "DAM_BREAK",
            AmbushEffectKind.Ingenuity => "INGENUITY",
            AmbushEffectKind.ChainClaw => "CHAIN_CLAW",
            AmbushEffectKind.ClawAssault => "CLAW_ASSAULT",
            AmbushEffectKind.AmbushPounce => "AMBUSH_POUNCE",
            AmbushEffectKind.ThroatGrip => "THROAT_GRIP",
            AmbushEffectKind.HoldBreath => "HOLD_BREATH",
            AmbushEffectKind.Hunt => "HUNT",
            AmbushEffectKind.WhetClaw => "WHET_CLAW",
            AmbushEffectKind.Blizzard => "BLIZZARD",
            AmbushEffectKind.DeepBite => "DEEP_BITE",
            AmbushEffectKind.FrostFangCard => "FROST_FANG_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };
        var text = new LocString("cards", "AMBUSH_BARRAGE.effect." + effect.Kind);
        text.Add("Name", new LocString("cards", id + ".title").GetFormattedText());
        text.Add("Amount", effect.Amount);
        text.Add("Secondary", effect.Secondary);
        if (effect.Kind == AmbushEffectKind.Hunt)
            text.Add("PreyName", new LocString("cards", "PREY.title").GetFormattedText() + (effect.Upgraded ? "+" : ""));
        return text.GetFormattedText();
    }

    public static async Task ResolveAsync(RecordedAmbush effect, AmbushBarrage source,
        PlayerChoiceContext context, CardPlay play, bool allEnemies, decimal damageBonus)
    {
        switch (effect.Kind)
        {
            case AmbushEffectKind.SneakAttack:
            case AmbushEffectKind.DamBreak:
                await AttackWithDebuffAsync<WeakPower>(effect, source, context, play, allEnemies, damageBonus);
                break;
            case AmbushEffectKind.ThroatGrip:
                await AttackWithDebuffAsync<VulnerablePower>(effect, source, context, play, allEnemies, damageBonus);
                break;
            case AmbushEffectKind.Ingenuity:
                await CreatureCmd.GainBlock(source.Owner.Creature, effect.Amount, ValueProp.Move, play);
                break;
            case AmbushEffectKind.ChainClaw:
                await PowerCmd.Apply<ChainClawDiscountPower>(context, source.Owner.Creature,
                    effect.Amount, source.Owner.Creature, source);
                break;
            case AmbushEffectKind.ClawAssault:
                await CardPileCmd.Draw(context, (int)effect.Amount, source.Owner);
                break;
            case AmbushEffectKind.HoldBreath:
                await PlayerCmd.GainEnergy(effect.Secondary, source.Owner);
                break;
            case AmbushEffectKind.Hunt:
                await Prey.CreateIntoHandAsync(source.Owner, source.CombatState!, effect.Upgraded);
                break;
            case AmbushEffectKind.Blizzard:
                await FrostbitePower.ApplyFrostToAllAsync(context,
                    source.CombatState!.HittableEnemies, effect.Amount, source.Owner.Creature, source);
                break;
            case AmbushEffectKind.DeepBite:
                if (play.Target is { IsAlive: true, Monster: { IntendsToAttack: false } })
                    await CreatureCmd.Stun(play.Target);
                break;
            case AmbushEffectKind.FrostFangCard:
                await CreatureCmdCompat.Damage(context, source.Owner.Creature, effect.Secondary,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, null, source, play);
                if (source.Owner.Creature.IsAlive)
                    await source.AttackAsync(context, play, effect.Amount + damageBonus, allEnemies);
                break;
            // These modify the containing resolution; they are never recursively dispatched.
            case AmbushEffectKind.AmbushPounce:
            case AmbushEffectKind.WhetClaw:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect));
        }
    }

    private static async Task AttackWithDebuffAsync<T>(RecordedAmbush effect, AmbushBarrage source,
        PlayerChoiceContext context, CardPlay play, bool allEnemies, decimal damageBonus) where T : PowerModel
    {
        // Pick a fallback only once, so a collected debuff and its attack share the same target.
        Creature? target = allEnemies ? null : source.ResolveAttackTarget(play);
        Creature[] targets = allEnemies ? source.CombatState!.HittableEnemies.ToArray()
            : target is { IsAlive: true } ? [target] : [];
        if (effect.Secondary > 0m)
            foreach (Creature enemy in targets)
                if (enemy.IsAlive)
                    await PowerCmd.Apply<T>(context, enemy, effect.Secondary, source.Owner.Creature, source);
        if (allEnemies || target is { IsAlive: true })
            await source.AttackAsync(context, play, effect.Amount + damageBonus, allEnemies, target);
    }
}
