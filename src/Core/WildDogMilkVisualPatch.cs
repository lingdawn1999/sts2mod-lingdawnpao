using System;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core;

/// <summary>
/// Power cards await their initial play-pile tween before PlayPowerCardFlyVfx.
/// Bypass that earlier visual wait for this card too; ordinary gameplay still
/// passes through the original OnPlayWrapper, power application and relic hooks.
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddDuringManualCardPlay))]
internal static class WildDogMilkManualEntryPatch
{
    private static bool Prefix(CardModel card, ref Task __result)
    {
        if (!WildDogMilkVisualPatch.AppliesTo(card)) return true;
        __result = EnterPlayAsync(card);
        return false;
    }

    private static async Task EnterPlayAsync(CardModel card)
    {
        if (CombatManager.Instance.IsOverOrEnding || card.Owner.Creature.IsDead) return;
        if (!TestMode.IsOn) Log.Info($"野生狗奶：手牌出牌开始，玩家={card.Owner.NetId}，原牌堆={card.Pile?.Type}");
        NCard? node = TestMode.IsOn ? null : NCard.FindOnTable(card);
        // The generic add path retains normal pile hooks and card ownership, but
        // does not create/await the power card's entry tween.
        await CardPileCmd.Add(card, PileType.Play, skipVisuals: true);
        if (card.Pile?.Type != PileType.Play) return;
        if (node != null && AmbushCardVisuals.CanAnimate(node, card))
        {
            try
            {
                var ui = NCombatRoom.Instance?.Ui;
                if (ui != null)
                {
                    if (ui.PlayQueue.IsAncestorOf(node)) ui.PlayQueue.RemoveCardFromQueueForExecution(card);
                    else if (ui.Hand.IsAncestorOf(node)) ui.Hand.Remove(card);
                    AmbushCardVisuals.CancelOwnedAnimation(node);
                    node.PlayPileTween?.Kill();
                    node.PlayPileTween = null;
                    if (node.GetParent() != ui.PlayContainer)
                    {
                        if (node.GetParent() != null) node.Reparent(ui.PlayContainer);
                        else ui.AddToPlayContainer(node);
                    }
                }
            }
            catch (Exception exception)
            {
                // Only optional visuals are recoverable here; never retry a
                // gameplay command or suppress a failed pile-change hook.
                Log.Warn($"野生狗奶：入场卡面异常，继续结算（{exception}）");
                if (AmbushCardVisuals.CanAnimate(node, card))
                {
                    AmbushCardVisuals.CancelOwnedAnimation(node);
                    node.QueueFreeSafely();
                }
            }
        }
        if (!TestMode.IsOn) Log.Info("野生狗奶：已进入出牌堆，不等待入场动画");
    }
}

/// <summary>
/// A bounded visual fallback for Wild Dog Milk. The standard power fly requires a live creature,
/// trail and initialized swoosh curve before OnPlay can run. Its renderer also owns the card's
/// cleanup (RemoveFromCombat intentionally skips power cards), so skipping it must clean the node.
/// No gameplay tasks are caught or retried here; resource spending and power application stay vanilla.
/// </summary>
[HarmonyPatch(typeof(CardModel), "PlayPowerCardFlyVfx")]
internal static class WildDogMilkVisualPatch
{
    internal static bool AppliesTo(CardModel card) => card is WildDogMilk;

    private static bool Prefix(CardModel __instance, ref Task __result)
    {
        if (!AppliesTo(__instance)) return true;
        __result = PlayAsync(__instance);
        return false;
    }

    private static async Task PlayAsync(CardModel card)
    {
        if (TestMode.IsOn) return;
        Log.Info("野生狗奶：开始卡面退场");
        NCard? node = null;
        Tween? fade = null;
        int animationEpoch = 0;
        try
        {
            node = NCard.FindOnTable(card, PileType.Play);
            if (node == null || !GodotObject.IsInstanceValid(node)) return;
            node.PlayPileTween?.Kill();
            node.PlayPileTween = null;
            fade = node.CreateTween();
            animationEpoch = AmbushCardVisuals.TrackAnimation(node, fade);
            fade.TweenProperty(node, "modulate:a", 0f, 0.2f);
            // Wait on the game clock, never on a tween-finished signal that can disappear with its node.
            await Cmd.CustomScaledWait(0.1f, 0.2f);
        }
        catch (Exception exception)
        {
            Log.Warn($"野生狗奶：卡面退场异常，继续能力牌结算（{exception}）");
        }
        finally
        {
            if (fade != null && GodotObject.IsInstanceValid(fade)) fade.Kill();
            if (node != null && AmbushCardVisuals.CanComplete(node, card, animationEpoch)) node.QueueFreeSafely();
            Log.Info("野生狗奶：卡面退场结束");
        }
    }
}
