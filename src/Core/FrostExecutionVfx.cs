using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core;

/// <summary>
/// Retains DoomKill's death prevention, hooks, body capture and DeathAnimationTask.
/// Only the rendering of a frost-owned NDoomVfx is replaced; ordinary Doom is untouched.
/// </summary>
internal static class FrostExecutionVfx
{
    internal const string ScriptPath = "res://lingdawnpao/vfx/frost_execution/FrostExecution.gd";
    internal const string DoomSound = "event:/sfx/characters/necrobinder/necrobinder_doom_kill";
    private static GDScript? _script;
    private static readonly AsyncLocal<Scope?> Current = new();
    private static readonly ConditionalWeakTable<NDoomVfx, object> OwnedEffects = new();

    internal sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        internal NCreatureVisuals Visuals { get; }
        internal bool Customized { get; set; }

        internal Scope(NCreatureVisuals visuals)
        {
            Visuals = visuals;
            _previous = Current.Value;
            Current.Value = this;
        }

        public void Dispose() => Current.Value = _previous;
    }

    internal static bool SuppressDoomPresentation => Current.Value?.Customized == true;

    public static bool Resolve() => AccessTools.Method(typeof(NDoomVfx), "PlayVfxInternal", Type.EmptyTypes)?.ReturnType == typeof(Task)
        && AccessTools.Method(typeof(NDoomVfx), nameof(NDoomVfx.Create),
            [typeof(NCreatureVisuals), typeof(Vector2), typeof(Vector2), typeof(bool)]) != null
        && AccessTools.Method(typeof(NDoomOverlayVfx), nameof(NDoomOverlayVfx.GetOrCreate)) != null;

    public static void Warmup()
    {
        try
        {
            FrostExecutionFeedback.Warmup();
            if (Resolve()) _script = ResourceLoader.Load<GDScript>(ScriptPath);
            if (_script == null || !_script.CanInstantiate())
            {
                _script = null;
                Log.Warn("冰冻处决：碎冰资源不可用，沿用本体处决表现");
            }
            else Log.Info("冰冻处决：碎冰特效已预热");
        }
        catch (Exception exception)
        {
            _script = null;
            Log.Warn($"冰冻处决：特效预热失败，沿用本体表现（{exception.Message}）");
        }
    }

    public static async Task DoomKillAsync(Creature target)
    {
        NCreatureVisuals? visuals = TestMode.IsOn ? null : NCombatRoom.Instance?.GetCreatureNode(target)?.Visuals;
        if (visuals == null || _script == null)
        {
            await DoomPower.DoomKill(new[] { target });
            return;
        }

        using var scope = new Scope(visuals);
        await DoomPower.DoomKill(new[] { target });
    }

    internal static void Tag(NCreatureVisuals visuals, bool shouldDie, NDoomVfx? effect)
    {
        Scope? scope = Current.Value;
        if (scope == null) return;
        // Reviving enemies and players retain the native non-disappearing branch.
        // Reset on every Create so nested, ordinary Doom cannot inherit the override.
        scope.Customized = shouldDie && effect != null && _script != null && scope.Visuals == visuals;
        if (scope.Customized) OwnedEffects.GetValue(effect!, static _ => new object());
    }

    internal static bool Owns(NDoomVfx effect) => OwnedEffects.TryGetValue(effect, out _);

    internal static async Task PlayCapturedAsync(NDoomVfx owner)
    {
        FrostExecutionLifetime? lifetime = null;
        Sprite2D? visual = null;
        Node2D? effect = null;
        try
        {
            if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree()) return;
            var viewport = owner.GetNode<SubViewport>("Viewport");
            visual = owner.GetNode<Sprite2D>("%Visual");
            // Match the native viewport sprite's origin, including the scene's Mask offset.
            visual.Position += Vector2.Up * viewport.Size.Y * 0.5f;
            foreach (string name in new[] { "DoomVfxBack", "DoomVfxFront" })
                if (owner.GetNodeOrNull<Node2D>(name) is { } particles) particles.Visible = false;
            lifetime = new FrostExecutionLifetime();
            owner.AddChild(lifetime);
            // Read back only after several actual process frames. Exiting the tree completes
            // both waits, including when the last enemy dies or the combat room is abandoned.
            if (!await lifetime.CaptureReady) return;
            if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree()) return;
            using Image image = viewport.GetTexture().GetImage();
            Rect2I used = image.GetUsedRect();
            if (used.Size.X <= 1 || used.Size.Y <= 1)
                throw new InvalidOperationException("敌人捕获画面为空");
            var texture = ImageTexture.CreateFromImage(image);
            effect = _script!.New().As<Node2D>();
            // Native sprite is centered horizontally, with its bottom at local y=0.
            effect.Position = new Vector2(used.Position.X + used.Size.X * 0.5f - viewport.Size.X * 0.5f,
                used.End.Y - viewport.Size.Y);
            owner.AddChild(effect);
            effect.Call("configure", texture, (Vector2)used.Size, (int)(owner.GetInstanceId() % 2147483647), true);
            if (effect.Get("fragments").AsGodotArray().Count == 0)
                throw new InvalidOperationException("未能生成碎冰网格");
            visual.Visible = false;
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            effect.Connect("cracked", Callable.From(() => FrostExecutionFeedback.Crack(lifetime)));
            effect.Connect("shattered", Callable.From(() => FrostExecutionFeedback.Shatter(lifetime)));
            FrostExecutionFeedback.Freeze(lifetime);
            lifetime.Play(effect);
            await lifetime.Finished;
        }
        catch (Exception exception)
        {
            Log.Warn($"冰冻处决：碎冰播放失败，结束特效以保证死亡结算（{exception.Message}）");
            // A failed readback must not leave the captured body invisible or a death task pending.
            if (GodotObject.IsInstanceValid(effect)) effect!.QueueFree();
            if (GodotObject.IsInstanceValid(visual) && GodotObject.IsInstanceValid(lifetime)
                && lifetime!.IsInsideTree())
            {
                visual!.Visible = true;
                lifetime.Fade(visual);
                await lifetime.Finished;
            }
        }
        finally
        {
            OwnedEffects.Remove(owner);
            // The skipped native PlayVfxInternal normally frees this viewport and captured body.
            if (GodotObject.IsInstanceValid(owner) && !owner.IsQueuedForDeletion()) owner.QueueFree();
        }
    }
}

[HarmonyPatch(typeof(NDoomVfx), nameof(NDoomVfx.Create))]
internal static class FrostExecutionCreatePatch
{
    private static void Postfix(NCreatureVisuals creatureVisuals, bool shouldDie, NDoomVfx? __result)
        => FrostExecutionVfx.Tag(creatureVisuals, shouldDie, __result);
}

[HarmonyPatch(typeof(NDoomVfx), "PlayVfxInternal")]
internal static class FrostExecutionRenderPatch
{
    private static bool Prefix(NDoomVfx __instance, ref Task __result)
    {
        if (!FrostExecutionVfx.Owns(__instance)) return true;
        __result = FrostExecutionVfx.PlayCapturedAsync(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(NDoomOverlayVfx), nameof(NDoomOverlayVfx.GetOrCreate))]
internal static class FrostExecutionOverlayPatch
{
    private static bool Prefix(ref NDoomOverlayVfx? __result)
    {
        if (!FrostExecutionVfx.SuppressDoomPresentation) return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
internal static class FrostExecutionSoundPatch
{
    private static bool Prefix(string sfx) => sfx != FrostExecutionVfx.DoomSound
        || !FrostExecutionVfx.SuppressDoomPresentation;
}
