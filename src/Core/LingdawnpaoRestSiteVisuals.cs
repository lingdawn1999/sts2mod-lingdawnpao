using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace lingdawnpao.Core;

/// <summary>Install the resting pose before native Ready chooses the act's loop and the room mirrors co-op slots.</summary>
internal static class LingdawnpaoRestSiteVisuals
{
    internal const string SkeletonPath = "res://animations/rest_site/lingdawnpao/rest_v2_motion/snow_leopard_rest_skel_data.tres";
    private static Resource? _skeleton;
    internal static void Warmup()
    {
        try { _skeleton ??= GD.Load<Resource>(SkeletonPath); }
        catch (Exception exception) { Log.Warn($"雪豹：火堆动画预热失败（{exception.Message}）"); }
    }

    internal static bool TryApply(NRestSiteCharacter avatar)
    {
        if (!GodotObject.IsInstanceValid(avatar) || avatar.IsQueuedForDeletion()
            || avatar.Player?.Character is not Lingdawnpao
            || avatar.GetNodeOrNull<Node2D>("SpineSprite") is not { } sprite
            || !sprite.IsClass("SpineSprite")) return false;
        var hitbox = avatar.GetNodeOrNull<Control>("ControlRoot/Hitbox");
        var reticle = avatar.GetNodeOrNull<Control>("ControlRoot/SelectionReticle");
        var leftThought = avatar.GetNodeOrNull<Control>("ControlRoot/ThoughtBubbleLeft");
        var rightThought = avatar.GetNodeOrNull<Control>("ControlRoot/ThoughtBubbleRight");
        if (hitbox == null || reticle == null || leftThought == null || rightThought == null) return false;
        Warmup();
        if (_skeleton == null || !GodotObject.IsInstanceValid(_skeleton)) return false;
        Resource? original = sprite.Get("skeleton_data_res").As<Resource>();
        if (original != null && original.GetInstanceId() == _skeleton.GetInstanceId()) return true;
        Vector2 originalScale = sprite.Scale;
        Vector2 originalPosition = sprite.Position;
        var controls = new[] { hitbox, reticle, leftThought, rightThought };
        var offsets = new List<float[]>();
        foreach (Control control in controls)
            offsets.Add([control.OffsetLeft, control.OffsetTop, control.OffsetRight, control.OffsetBottom]);
        try
        {
            sprite.Set("skeleton_data_res", Variant.From(_skeleton));
            sprite.Scale = new Vector2(originalScale.X < 0 ? -1.15f : 1.15f, 1.15f);
            // Native rest poses are centered above a lower ground plane. Match
            // that baseline while leaving the room's parent transforms intact.
            sprite.Position = new Vector2(0, 280);
            Bounds(hitbox, -365, -80, 330, 335);
            Bounds(reticle, -365, -80, 330, 335);
            Bounds(leftThought, -80, -130, -80, -130);
            Bounds(rightThought, 210, -130, 210, -130);
            // Native _Ready starts overgrowth_loop / hive_loop / glory_loop.
            // It also owns _tracks/light_off, input, option bubbles and exit cleanup.
            return true;
        }
        catch (Exception exception)
        {
            if (GodotObject.IsInstanceValid(sprite))
            {
                sprite.Set("skeleton_data_res", Variant.From(original));
                sprite.Scale = originalScale;
                sprite.Position = originalPosition;
            }
            for (int i = 0; i < controls.Length; i++)
            {
                float[] previous = offsets[i];
                Bounds(controls[i], previous[0], previous[1], previous[2], previous[3]);
            }
            Log.Warn($"雪豹：火堆动画替换失败，恢复原视觉（{exception.Message}）");
            return false;
        }
    }

    private static void Bounds(Control control, float left, float top, float right, float bottom)
    {
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }
}

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
internal static class NRestSiteCharacter_LingdawnpaoVisualsPatch
{
    // Ready is large enough to avoid the early-JIT inlining risk of the small Create wrapper.
    private static void Prefix(NRestSiteCharacter __instance)
    {
        try { LingdawnpaoRestSiteVisuals.TryApply(__instance); }
        catch (Exception exception) { Log.Warn($"雪豹：跳过本次火堆视觉替换（{exception.Message}）"); }
    }
}
