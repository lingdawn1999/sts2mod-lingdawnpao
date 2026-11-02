using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace lingdawnpao.Core;

/// <summary>Replace only the snow leopard's existing merchant sprite, preserving the native room and callbacks.</summary>
internal static class LingdawnpaoMerchantVisuals
{
    internal const string SkeletonPath = "res://animations/merchant/lingdawnpao/shop_v2_motion/snow_leopard_shop_skel_data.tres";
    private static Resource? _skeleton;
    internal static void Warmup()
    {
        try { _skeleton ??= GD.Load<Resource>(SkeletonPath); }
        catch (Exception exception) { Log.Warn($"雪豹：商店动画预热失败（{exception.Message}）"); }
    }

    internal static bool TryApply(Player player, NMerchantCharacter avatar)
    {
        if (player.Character is not Lingdawnpao || !GodotObject.IsInstanceValid(avatar)
            || avatar.IsQueuedForDeletion() || avatar.GetChildCount() == 0
            || avatar.GetChild(0) is not Node2D sprite || !sprite.IsClass("SpineSprite")) return false;
        Warmup();
        if (_skeleton == null || !GodotObject.IsInstanceValid(_skeleton)) return false;
        Resource? original = sprite.Get("skeleton_data_res").As<Resource>();
        if (original != null && original.GetInstanceId() == _skeleton.GetInstanceId()) return true;
        Vector2 originalScale = sprite.Scale;
        Vector2 originalPosition = sprite.Position;
        void Restore()
        {
            if (!GodotObject.IsInstanceValid(sprite) || sprite.IsQueuedForDeletion()) return;
            sprite.Set("skeleton_data_res", Variant.From(original));
            sprite.Scale = originalScale;
            sprite.Position = originalPosition;
        }
        try
        {
            sprite.Set("skeleton_data_res", Variant.From(_skeleton));
            sprite.Scale = Vector2.One * .64f;
            sprite.Position = Vector2.Zero;
            var controller = new MegaSprite(Variant.From(sprite));
            avatar.RunWhenSpineReady(controller, _ =>
            {
                try
                {
                    if (!controller.HasAnimation("relaxed_loop") || !controller.HasAnimation("die"))
                        throw new InvalidOperationException("缺少商店待机或死亡动作");
                    // Let the native merchant wrapper select its own branch-specific
                    // Spine overload and normal randomized idle phase.
                    avatar.PlayAnimation("relaxed_loop", loop: true);
                }
                catch (Exception exception)
                {
                    Restore();
                    Log.Warn($"雪豹：商店动画未就绪，恢复原视觉（{exception.Message}）");
                }
            });
            return true;
        }
        catch (Exception exception)
        {
            Restore();
            Log.Warn($"雪豹：商店动画替换失败，恢复原视觉（{exception.Message}）");
            return false;
        }
    }

    internal static void Apply(IReadOnlyList<Player> players, IReadOnlyList<NMerchantCharacter> avatars)
    {
        // The room sorts its player list before building avatars. Use that exact order,
        // including co-op rows, rather than inferring the owner from screen positions.
        for (int i = 0; i < Math.Min(players.Count, avatars.Count); i++)
        {
            try { TryApply(players[i], avatars[i]); }
            catch (Exception exception) { Log.Warn($"雪豹：跳过本次商店视觉替换（{exception.Message}）"); }
        }
    }
}

[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class NMerchantRoom_LingdawnpaoVisualsPatch
{
    private static void Postfix(NMerchantRoom __instance, List<Player> ____players) =>
        LingdawnpaoMerchantVisuals.Apply(____players, __instance.PlayerVisuals);
}
