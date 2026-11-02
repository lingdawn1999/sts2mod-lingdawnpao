using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace lingdawnpao.Core;

/// <summary>Invalidates callbacks when a pooled node changes visual owner, even if the same model is reused.</summary>
internal sealed class CardVisualEpoch
{
    private int _value;
    public int Renew() => ++_value;
    public bool IsCurrent(int value) => value == _value;
}

internal static class AmbushCardVisuals
{
    private sealed class AnimationState
    {
        public readonly CardVisualEpoch Epoch = new();
        public Tween? Tween;
    }
    private static readonly ConditionalWeakTable<NCard, AnimationState> Animations = new();

    public static void CancelOwnedAnimation(NCard card)
    {
        AnimationState state = Animations.GetOrCreateValue(card);
        state.Epoch.Renew();
        if (state.Tween is { } tween && GodotObject.IsInstanceValid(tween)) tween.Kill();
        if (ReferenceEquals(card.PlayPileTween, state.Tween)) card.PlayPileTween = null;
        state.Tween = null;
    }

    public static int TrackAnimation(NCard card, Tween tween)
    {
        CancelOwnedAnimation(card);
        AnimationState state = Animations.GetOrCreateValue(card);
        state.Tween = tween;
        card.PlayPileTween = tween; // Also lets the game's ordinary pool cleanup cancel it.
        return state.Epoch.Renew();
    }

    public static bool CanAnimate(NCard card, CardModel model) => GodotObject.IsInstanceValid(card)
        && !card.IsQueuedForDeletion() && ReferenceEquals(card.Model, model) && model.Pile?.Type != PileType.Hand;

    public static bool CanComplete(NCard card, CardModel model, int epoch) => CanAnimate(card, model)
        && Animations.TryGetValue(card, out var state) && state.Epoch.IsCurrent(epoch);

    public static void RestoreForPool(NCard card)
    {
        CancelOwnedAnimation(card);
        RestoreVisibility(card);
        card.Scale = Vector2.One;
        card.Rotation = 0f;
        card.Position = Vector2.Zero;
    }

    public static void RestoreForHand(NCard card)
    {
        CancelOwnedAnimation(card);
        RestoreVisibility(card);
        card.Scale = Vector2.One;
    }

    private static void RestoreVisibility(NCard card)
    {
        card.Modulate = Colors.White;
        card.SelfModulate = Colors.White;
        card.Visible = true;
        if (card.Body is { } body && GodotObject.IsInstanceValid(body))
        {
            body.Modulate = Colors.White;
            body.SelfModulate = Colors.White;
            body.Visible = true;
            body.Scale = Vector2.One;
        }
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.OnFreedToPool))]
internal static class NCard_OnFreedToPool_CancelAmbushAnimationPatch
{
    private static void Prefix(NCard __instance) => AmbushCardVisuals.CancelOwnedAnimation(__instance);
}

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand.Add))]
internal static class NPlayerHand_Add_RestoreAmbushCardPatch
{
    private static void Prefix(NCard __0)
    {
        if (__0.Model is ILingdawnpaoCard) AmbushCardVisuals.RestoreForHand(__0);
    }
}

/// <summary>Each queued burial owns its own rollback entry; finishing one must not clear another.</summary>
internal sealed class PendingBuryEntries<T> where T : class
{
    private readonly Dictionary<(ulong Player, uint Card), T> _entries = new();
    public void Register(ulong player, uint card, T value) => _entries[(player, card)] = value;
    public T? Take(ulong player, uint card) => _entries.Remove((player, card), out var value) ? value : null;
    public void Clear(ulong player)
    {
        var keys = new List<(ulong Player, uint Card)>(_entries.Keys);
        foreach (var key in keys) if (key.Player == player) _entries.Remove(key);
    }
}
