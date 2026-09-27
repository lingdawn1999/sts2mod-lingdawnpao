using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 芝士雪豹专属雪花能量：完全用代码复刻本体 NEnergyCounter 场景的节点结构。
    /// 不走自定义 .tscn —— 本项目已验证 mod 场景引用本体 C# 脚本（NEnergyCounter/MegaLabel）
    /// 导出时会烘焙成空类名，运行时实例化成裸节点导致战斗黑屏。
    /// 只需一张雪花图（六出旋转对称、中心实心盘）：卡牌费用图标与战斗计数器共用，
    /// 旋转由本体 NEnergyCounter._Process 驱动（30°/s）；背后光晕用渐变纹理代码生成。
    /// 雪花图缺失则整体回退到本体，保证游戏不会坏。
    /// </summary>
    public static class LingdawnpaoEnergyCounter
    {
        public const string SnowflakePath =
            "res://images/ui/combat/energy_counters/lingdawnpao/lingdawnpao_snowflake.png";

        // 卡牌费用徽章 / 联机面板等复用同一纹理，懒加载缓存。
        // ⚠️ 缓存必须**自愈**：本体（或别的 mod）重新加载/驱逐资源时会把这张贴图释放掉，
        // 而我们是长期持有静态引用的那一方——一旦它被释放，我们再把这张"已销毁"的贴图交给
        // 本体的 `NCard.Reload` 去 `SetTexture`，就会抛 ObjectDisposedException，
        // 让整个 Reload **半途中断**（卡面停在上一张的状态：类型标签、文案全是旧值）。
        // 2026-09-22 实测：单次会话里 17 次该异常，卡组界面里「防御」显示成 Attack 就是这么来的。
        // 所以取用前先校验实例是否还有效，失效就丢掉重载。
        public static Texture2D? Texture
        {
            get
            {
                if (_texture != null && GodotObject.IsInstanceValid(_texture))
                {
                    return _texture;
                }

                _texture = null;
                if (ResourceLoader.Exists(SnowflakePath))
                {
                    _texture = ResourceLoader.Load<Texture2D>(SnowflakePath, null,
                        ResourceLoader.CacheMode.Reuse);
                }

                return _texture;
            }
        }

        private static Texture2D? _texture;

        private static readonly FieldInfo? PlayerField =
            typeof(NEnergyCounter).GetField("_player",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo? ParticlesField =
            typeof(NParticlesContainer).GetField("_particles",
                BindingFlags.NonPublic | BindingFlags.Instance);

        public static NEnergyCounter? TryCreate(Player player, NEnergyCounter? original)
        {
            // TestMode 下本体返回 null；雪花图没放也保持本体行为
            if (original is null || player.Character is not Lingdawnpao)
            {
                return original;
            }

            if (!ResourceLoader.Exists(SnowflakePath))
            {
                Log.Info("[芝士雪豹] 自制雪花能量图未放置，使用本体能量球");
                return original;
            }

            try
            {
                var root = new NEnergyCounter
                {
                    Name = "LingdawnpaoEnergyCounter",
                    OffsetRight = 128f,
                    OffsetBottom = 128f,
                    PivotOffset = new Vector2(64f, 64f),
                };

                BuildVfx(root, "EnergyVfxBack");
                BuildLayers(root);
                BuildVfx(root, "EnergyVfxFront");
                BuildLabel(root, player);

                PlayerField?.SetValue(root, player);
                Log.Info("[芝士雪豹] 自制雪花能量创建成功");
                return root;
            }
            catch (Exception e)
            {
                Log.Info($"[芝士雪豹] 自制雪花能量创建失败，回退本体：{e.Message}");
                return original;
            }
        }

        private static void BuildVfx(Control root, string uniqueName)
        {
            // 本体获得能量时会对 vfx 调 Restart()；不给粒子数组会空引用，
            // 塞一个空数组让 Restart 空转（自制能量不需要粒子特效）。
            var vfx = new NParticlesContainer
            {
                Name = uniqueName,
                Position = new Vector2(64f, 64f),
            };
            vfx.SetUniqueNameInOwner(true);
            ParticlesField?.SetValue(vfx, new Godot.Collections.Array<GpuParticles2D>());
            root.AddChild(vfx);
            vfx.Owner = root;
        }

        private static void BuildLayers(Control root)
        {
            var layers = NewFullRect("Layers", unique: true);
            root.AddChild(layers);
            layers.Owner = root;

            // 静态底层：代码生成的柔冰蓝光晕，让雪花分叉在任何背景上都清楚
            var glow = new GradientTexture2D
            {
                Width = 128,
                Height = 128,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
                Gradient = new Gradient(),
            };
            glow.Gradient.SetColor(0, new Color(0.55f, 0.78f, 0.95f, 0.55f));
            glow.Gradient.SetColor(1, new Color(0.55f, 0.78f, 0.95f, 0f));
            var glowRect = new TextureRect
            {
                Name = "Glow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Texture = glow,
            };
            glowRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layers.AddChild(glowRect);
            glowRect.Owner = root;

            var rotationLayers = NewFullRect("RotationLayers", unique: true);
            layers.AddChild(rotationLayers);
            rotationLayers.Owner = root;

            // 本体 _Process：第 i 个旋转层以 30°/s×(i+1) 旋转。只放一个 → 30°/s 慢旋
            var snowflake = new TextureRect
            {
                Name = "Snowflake",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                PivotOffset = new Vector2(64f, 64f),
                Texture = ResourceLoader.Load<Texture2D>(SnowflakePath, null,
                    ResourceLoader.CacheMode.Reuse),
            };
            snowflake.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rotationLayers.AddChild(snowflake);
            snowflake.Owner = root;
        }

        private static void BuildLabel(Control root, Player player)
        {
            var label = new MegaLabel
            {
                Name = "Label",
                Text = "3/3",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinFontSize = 32,
                MaxFontSize = 36,
            };
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.OffsetLeft = 16f;
            label.OffsetTop = -29f;
            label.OffsetRight = -16f;
            label.OffsetBottom = 29f;

            var baseFont = ResourceLoader.Load<Font>("res://themes/kreon_bold_shared.tres");
            if (baseFont != null)
            {
                label.AddThemeFontOverride(ThemeConstants.Label.Font, new FontVariation
                {
                    BaseFont = baseFont,
                    SpacingGlyph = 2,
                });
            }
            label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 36);
            label.AddThemeColorOverride(ThemeConstants.Label.FontColor,
                new Color(1f, 0.964706f, 0.886275f));
            label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor,
                new Color(0f, 0f, 0f, 0.188235f));
            label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor,
                player.Character.EnergyLabelOutlineColor);
            label.AddThemeConstantOverride("shadow_offset_x", 3);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 16);
            label.AddThemeConstantOverride("shadow_outline_size", 16);

            root.AddChild(label);
            label.Owner = root;
        }

        private static Control NewFullRect(string name, bool unique)
        {
            var control = new Control
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            if (unique)
            {
                control.SetUniqueNameInOwner(true);
            }
            return control;
        }
    }
}
