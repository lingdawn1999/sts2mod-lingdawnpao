using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace lingdawnpao.Core;

// One entry per card identity, in first-trigger order. No card references or recursive barrage entries.
public enum AmbushEffectKind
{
    SneakAttack, Ingenuity, ChainClaw, ClawAssault, AmbushPounce, ThroatGrip,
    HoldBreath, Hunt, WhetClaw, Blizzard, DeepBite, FrostFangCard, DamBreak,
}

public sealed record RecordedAmbush(AmbushEffectKind Kind, decimal Amount = 0m,
    decimal Secondary = 0m, bool Upgraded = false);

public sealed class AmbushEffectLedger
{
    private readonly List<RecordedAmbush> _effects = new();
    public int Count => _effects.Count;

    // A later upgraded trigger improves the stored effect without adding a second name.
    public bool Record(RecordedAmbush effect)
    {
        int index = _effects.FindIndex(old => old.Kind == effect.Kind);
        if (index < 0)
        {
            _effects.Add(effect);
            return true;
        }
        if (effect.Upgraded && !_effects[index].Upgraded) _effects[index] = effect;
        return false;
    }

    public RecordedAmbush[] Snapshot() => _effects.ToArray();
    public void Clear() => _effects.Clear();

    // Whet Claw repeats a complete resolution once, never repeats its own repeat instruction.
    public static int ResolutionCount(IEnumerable<RecordedAmbush> effects) =>
        effects.Any(effect => effect.Kind == AmbushEffectKind.WhetClaw) ? 2 : 1;

    /// <summary>The runtime barrage loop, separated from scene-dependent commands for regression checks.</summary>
    public static async Task ResolveAsync(RecordedAmbush[] effects, int triggers,
        Func<Task> attack, Func<RecordedAmbush, Task> resolveBonus, Func<bool> shouldStop)
    {
        int resolutions = ResolutionCount(effects);
        for (int pass = 0; pass < resolutions; pass++)
        {
            if (shouldStop()) return;
            await attack();
            for (int trigger = 0; trigger < triggers; trigger++)
            {
                foreach (RecordedAmbush effect in effects)
                {
                    if (shouldStop()) return;
                    // Pounce shapes attacks; Whet Claw controls the outer loop. Neither is a recursive bonus.
                    if (effect.Kind is AmbushEffectKind.AmbushPounce or AmbushEffectKind.WhetClaw) continue;
                    await resolveBonus(effect);
                }
            }
        }
    }
}
