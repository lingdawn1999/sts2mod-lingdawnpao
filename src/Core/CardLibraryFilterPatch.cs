using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 卡牌百科（卡牌库）的角色筛选栏是场景里硬编码的 8 个按钮，没有 mod 扩展点。
    /// 做法（学自 BaseLib.CustomPoolFilters）：_Ready 后从零构建一个 NCardPoolFilter
    /// （Image 图标 + HSV 染色材质 + Shadow 影子层 + SelectionReticle 选中框，Owner 指向按钮本体
    /// 以支持 % 唯一名解析），插到死灵筛选之后，连接 Toggled/FocusEntered，注册进 _poolFilters。
    /// 9 个按钮在 4 列 GridContainer 下高 192px，与 BaseLib 的上限一致，无需缩放。
    /// </summary>
    [HarmonyPatch(typeof(NCardLibrary), "_Ready")]
    internal static class NCardLibrary_LingdawnpaoFilterPatch
    {
        private static bool _injected;

        private static void Postfix(NCardLibrary __instance)
        {
            if (_injected)
            {
                return;
            }

            try
            {
                var poolFilters = (Dictionary<NCardPoolFilter, Func<CardModel, bool>>)AccessTools
                    .Field(typeof(NCardLibrary), "_poolFilters").GetValue(__instance)!;
                var necrobinder = (NCardPoolFilter)AccessTools.Field(typeof(NCardLibrary), "_necrobinderFilter")
                    .GetValue(__instance)!;
                var lastHovered = AccessTools.Field(typeof(NCardLibrary), "_lastHoveredControl");

                var filter = new NCardPoolFilter
                {
                    Name = "LingdawnpaoPool",
                    CustomMinimumSize = new Vector2(64f, 64f),
                    Size = new Vector2(64f, 64f),
                    FocusMode = Control.FocusModeEnum.All,
                };

                Texture2D? icon = ResourceLoader.Load<Texture2D>(
                    "res://images/ui/top_panel/character_icon_lingdawnpao.png");
                if (icon == null)
                {
                    Log.Info("卡牌百科：雪豹图标缺失，跳过筛选按钮注入");
                    return;
                }

                var image = new TextureRect
                {
                    Name = "Image",
                    Texture = icon,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(56f, 56f),
                    Size = new Vector2(56f, 56f),
                    Position = new Vector2(4f, 4f),
                    Scale = new Vector2(0.9f, 0.9f),
                    PivotOffset = new Vector2(28f, 28f),
                    Material = MakeHsvMaterial(),
                };
                var shadow = new TextureRect
                {
                    Name = "Shadow",
                    Texture = icon,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(56f, 56f),
                    Size = new Vector2(56f, 56f),
                    Position = new Vector2(4f, 3f),
                    PivotOffset = new Vector2(28f, 28f),
                    ShowBehindParent = true,
                    Modulate = new Color(0f, 0f, 0f, 0.25f),
                };
                image.AddChild(shadow);

                var reticle = PreloadManager.Cache
                    .GetScene(SceneHelper.GetScenePath("ui/selection_reticle"))
                    .Instantiate<NSelectionReticle>();
                reticle.Name = "SelectionReticle";
                reticle.UniqueNameInOwner = true;

                filter.AddChild(image);
                image.Owner = filter;
                filter.AddChild(reticle);
                reticle.Owner = filter;

                Control necroControl = necrobinder;
                necroControl.AddSibling(filter);
                filter.Owner = necroControl.Owner;

                MethodInfo update = AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter")!;
                filter.Connect(NCardPoolFilter.SignalName.Toggled,
                    Callable.From<NCardPoolFilter>(f => update.Invoke(__instance, new object[] { f })));
                filter.Connect(Control.SignalName.FocusEntered, Callable.From(() =>
                    lastHovered.SetValue(__instance, filter)));

                poolFilters[filter] = static (CardModel c) => c.Pool is LingdawnpaoCardPool;
                // 记下来给 OnSubmenuOpened 的兼容补丁用（本体按 5 个本体角色预填了 _cardPoolFilters，
                // 雪豹用运行时角色实例去索引会抛 KeyNotFoundException —— 见 CharacterCompatPatches.cs）
                LingdawnpaoCompat.OurCardPoolFilter = filter;
                _injected = true;
                Log.Info("卡牌百科：雪豹筛选按钮注入成功");
            }
            catch (Exception e)
            {
                Log.Info($"卡牌百科：雪豹筛选按钮注入失败，跳过（{e}）");
            }
        }

        private static ShaderMaterial MakeHsvMaterial()
        {
            var shader = ResourceLoader.Load<Shader>("res://shaders/hsv.gdshader");
            var material = new ShaderMaterial { Shader = shader };
            // 初始为未选中态的灰暗配色；点击后 OnToggle 会按选中/未选中自动改 s/v
            material.SetShaderParameter("h", 1f);
            material.SetShaderParameter("s", 0.3f);
            material.SetShaderParameter("v", 0.55f);
            return material;
        }
    }
}
