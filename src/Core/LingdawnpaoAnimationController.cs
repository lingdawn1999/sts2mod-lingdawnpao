using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core;

/// <summary>Visual state only: never owns card resolution, energy or an awaited gameplay task.</summary>
internal static class LingdawnpaoAnimationController
{
    private sealed class OwnerState
    {
        internal required Creature Owner;
        internal required MegaSprite Sprite;
        internal required AnimState[] StandingReturns;
        internal required AnimState[] HiddenReturns;
        internal required AnimState Idle;
        internal required AnimState LowIdle;
        internal required AnimState Hidden;
        internal required AnimState HiddenLow;
        internal Creature LiveOwner => Owner.Player?.Creature ?? Owner;
        internal bool Low => LiveOwner.IsAlive && LiveOwner.MaxHp > 0
            && LiveOwner.CurrentHp * 4L <= LiveOwner.MaxHp;
        internal AnimState Rest(bool hidden) => hidden ? (Low ? HiddenLow : Hidden) : (Low ? LowIdle : Idle);
        internal void RefreshReturns()
        {
            foreach (var state in StandingReturns) state.NextState = Rest(false);
            foreach (var state in HiddenReturns) state.NextState = Rest(true);
        }
    }
    private static readonly ConditionalWeakTable<CreatureAnimator, OwnerState> Owners = new();
    internal static Creature? FindCreature(MegaSprite controller)
    {
        for (Node? node = controller.BoundObject as Node; node != null; node = node.GetParent())
            if (node is NCreature creature) return creature.Entity;
        return null;
    }

    internal static bool IsAmbushing(Creature? creature) => creature?.Player is { } player
        && AmbushZone.For(player).IsAmbushMode;

    internal static CreatureAnimator Create(MegaSprite controller, Creature? creature)
    {
        bool Ambush() => IsAmbushing(creature);
        var idle = new AnimState("idle_loop", isLooping: true);
        var low = new AnimState("low_health_loop", isLooping: true);
        var hidden = new AnimState("ambush_idle", isLooping: true);
        var hiddenLow = new AnimState("ambush_low_health_loop", isLooping: true);
        var enter = new AnimState("ambush_enter") { NextState = hidden };
        var exit = new AnimState("ambush_exit") { NextState = idle };
        var attack = new AnimState("attack") { NextState = idle };
        var cast = new AnimState("cast") { NextState = idle };
        var hurt = new AnimState("hurt") { NextState = idle };
        var ambushHurt = new AnimState("ambush_hurt") { NextState = hidden };
        var palm = new AnimState("ground_palm") { NextState = idle };
        var owner = new OwnerState { Owner = creature!, Sprite = controller,
            Idle = idle, LowIdle = low, Hidden = hidden, HiddenLow = hiddenLow,
            StandingReturns = [exit, attack, cast, hurt, palm], HiddenReturns = [enter, ambushHurt] };
        bool Low() => creature != null && owner.Low;
        if (creature != null) owner.RefreshReturns();
        var animator = new CreatureAnimator(creature != null ? owner.Rest(Ambush()) : idle, controller);
        foreach (string trigger in new[] { "Idle", "Relaxed", "Revive" })
        {
            animator.AddAnyState(trigger, hiddenLow, () => Ambush() && Low());
            animator.AddAnyState(trigger, hidden, Ambush);
            animator.AddAnyState(trigger, low, Low);
            animator.AddAnyState(trigger, idle);
        }
        animator.AddAnyState("AmbushEnter", enter);
        animator.AddAnyState("AmbushExit", exit);
        animator.AddAnyState("AmbushIdle", hiddenLow, Low);
        animator.AddAnyState("AmbushIdle", hidden);
        foreach (string trigger in new[] { "Attack", "Cast", "PowerUp", "GroundPalm" })
            animator.AddAnyState(trigger, hidden, Ambush);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("GroundPalm", palm);
        animator.AddAnyState("Cast", cast);
        animator.AddAnyState("PowerUp", cast);
        // A hit does not make the ambushing leopard stand up. Death remains a terminal state.
        animator.AddAnyState("Hit", ambushHurt, Ambush);
        animator.AddAnyState("Hit", hurt);
        animator.AddAnyState("Dead", new AnimState("ambush_die"), Ambush);
        animator.AddAnyState("Dead", new AnimState("die"));
        if (creature != null)
        {
            Owners.Add(animator, owner);
            // The native HP event runs after its value changes. Only idle clips may be
            // replaced here; a hit, attack or terminal death must finish normally.
            Action<int, int> changed = (_, _) => SyncHealth(animator);
            creature.CurrentHpChanged += changed;
            if (controller.BoundObject is Node node)
                node.TreeExiting += () => creature.CurrentHpChanged -= changed;
        }
        return animator;
    }

    internal static void RefreshReturns(CreatureAnimator animator)
    {
        if (Owners.TryGetValue(animator, out var owner)) owner.RefreshReturns();
    }

    internal static void SyncHealth(CreatureAnimator animator)
    {
        if (!Owners.TryGetValue(animator, out var owner) || owner.LiveOwner.IsDead) return;
        try
        {
            owner.RefreshReturns();
            string? current = owner.Sprite.GetAnimationState().GetCurrentAnimationName();
            if (current is not ("idle_loop" or "low_health_loop" or "ambush_idle" or "ambush_low_health_loop")) return;
            if (current != owner.Rest(IsAmbushing(owner.LiveOwner)).Id) animator.SetTrigger("Idle");
        }
        catch (Exception exception) { Log.Warn($"雪豹：战损待机切换失败（{exception.Message}）"); }
    }

    public static void SyncMode(Player player)
    {
        if (TestMode.IsOn || player.Creature.IsDead) return;
        try
        {
            NCreature? node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            if (node == null || !node.SpineAnimation.IsValid) return;
            string? current = node.SpineAnimation.GetCurrentTrack()?.GetAnimationName();
            bool hidden = current?.StartsWith("ambush_", StringComparison.Ordinal) == true;
            bool wanted = AmbushZone.For(player).IsAmbushMode;
            if (wanted == hidden) return;
            node.SetAnimationTrigger(wanted ? "AmbushEnter" : "AmbushExit");
        }
        catch (Exception exception)
        {
            Log.Warn($"雪豹：形态动画切换失败，不影响伏击状态（{exception.Message}）");
        }
    }

    internal static bool ShouldSuppress(Creature creature, string trigger)
    {
        if (creature.Player?.Character is not Lingdawnpao) return false;
        // Late damage/card callbacks must not interrupt a terminal death animation.
        if (creature.IsDead) return trigger is not ("Dead" or "Revive");
        return IsAmbushing(creature) && trigger is "Attack" or "Cast" or "PowerUp" or "GroundPalm";
    }

    internal static bool ShouldSuppress(CreatureAnimator animator, string trigger) =>
        Owners.TryGetValue(animator, out OwnerState? owner)
        && ShouldSuppress(owner.LiveOwner, trigger);
}

[HarmonyPatch(typeof(CreatureAnimator), nameof(CreatureAnimator.SetTrigger))]
internal static class LingdawnpaoAmbushAnimationPatch
{
    // NCreature.SetAnimationTrigger is a tiny wrapper and can be inlined before
    // mod loading. Guard the state machine itself, including direct animator calls.
    private static bool Prefix(CreatureAnimator __instance, string trigger)
    {
        LingdawnpaoAnimationController.RefreshReturns(__instance);
        return !LingdawnpaoAnimationController.ShouldSuppress(__instance, trigger);
    }
}

// A heal can occur while a transient clip is already queued. Correct its idle
// successor as it starts, without depending on beta-only conditional AnimState APIs.
[HarmonyPatch(typeof(CreatureAnimator), "OnAnimationStarted")]
internal static class LingdawnpaoLowHealthAnimationPatch
{
    private static void Postfix(CreatureAnimator __instance) =>
        LingdawnpaoAnimationController.SyncHealth(__instance);
}
