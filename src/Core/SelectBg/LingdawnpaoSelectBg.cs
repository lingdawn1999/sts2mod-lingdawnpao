using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 分层动态选人背景：背景板 + 雪豹抠图层 + 前后两层飘雪，纯代码构建
    /// （占位裸 Control 场景 scenes/screens/char_select/char_select_bg_lingdawnpao.tscn
    /// 无脚本引用所以导出安全，运行时整体替换成动态节点）。
    /// 鼠标视差（背景反向、雪豹同向）、雪豹层呼吸缩放+浮动、绒毛 shader 微摆。
    /// 眨眼/抖耳 = 整层贴图帧切换（Spine 翻书同机制）：往 BlinkFramePaths/TwitchFramePath
    /// 加新图即可扩展动画帧；帧缺失时对应效果自动禁用。变体图需与底图同帧同位，
    /// 且已做色彩配准（否则换帧会跳色温）。
    /// </summary>
    public partial class LingdawnpaoSelectBg : Control
    {
        private const string BgPath = "res://images/char_select/poster_bg.png";
        private const string LeopardPath = "res://images/char_select/poster_leopard.png";
        private const string ClosedEyesPath = "res://images/char_select/poster_leopard_closed.png";
        private const string EarsVariantPath = "res://images/char_select/poster_leopard_ears.png";

        // 绒毛微摆区域（全图 UV，海报 2048x1152）：左腮 x955 y680 半径 85x130，右腮 x1375 y680，胸 x1190 y990
        private static readonly Vector4 Sway0Rect = new(955f / 2048f, 680f / 1152f, 85f / 2048f, 130f / 1152f);
        private static readonly Vector4 Sway1Rect = new(1375f / 2048f, 680f / 1152f, 85f / 2048f, 130f / 1152f);
        private static readonly Vector4 Sway2Rect = new(1190f / 2048f, 990f / 1152f, 160f / 2048f, 150f / 1152f);

        private const string ShaderSource = """
            shader_type canvas_item;

            uniform vec4 sway0 = vec4(0.0); // xy=UV 中心，zw=UV 半径
            uniform vec4 sway1 = vec4(0.0);
            uniform vec4 sway2 = vec4(0.0);
            uniform float sway_amp = 9.0;
            uniform vec2 sway_texel = vec2(0.00048828125, 0.00086805556); // 1/2048, 1/1152（uniform 默认值必须字面常量，不能写除法）

            float sway_weight(vec2 uv, vec4 region) {
                if (region.z <= 0.0) {
                    return 0.0;
                }
                vec2 f = (uv - region.xy) / region.zw;
                return smoothstep(1.0, 0.55, length(f));
            }

            void fragment() {
                float w0 = sway_weight(UV, sway0);
                float w1 = sway_weight(UV, sway1);
                float w2 = sway_weight(UV, sway2);
                vec2 off = vec2(0.0);
                off.x += sin(TIME * 1.1) * sway_amp * w0;
                off.x += sin(TIME * 0.9 + 1.7) * sway_amp * w1;
                off.x += sin(TIME * 0.7 + 3.9) * sway_amp * w2 * 0.6;
                off.y += (sin(TIME * 0.8 + 2.2) * 0.7 + sin(TIME * 1.7) * 0.3) * sway_amp * w2;
                COLOR = texture(TEXTURE, UV + off * sway_texel);
            }
            """;

        private TextureRect? _bgRect;
        private Control? _leopardRoot;
        private TextureRect? _leopardRect;
        private ShaderMaterial? _leopardMaterial;
        private CpuParticles2D? _snowFar;
        private CpuParticles2D? _snowNear;
        private readonly Random _rng = new();

        private Texture2D? _baseLeopard;
        private Texture2D? _closedEyes;
        private Texture2D? _earsVariant;

        // 眨眼与抖耳共用一条换帧队列：触发时按顺序入队 (贴图, 时长)，播完自动回底图
        private readonly List<(Texture2D Frame, double Duration)> _swapFrames = [];
        private int _swapFrame;
        private double _frameLeft;
        private double _nextBlinkIn = 2.5;
        private double _nextTwitchIn = 5.0;
        private Vector2 _parallax;
        private double _time;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            Texture2D? bg = ResourceLoader.Load<Texture2D>(BgPath);
            if (bg == null)
            {
                Log.Info("芝士雪豹：背景板贴图缺失，动态背景空载");
                return;
            }

            _bgRect = new TextureRect
            {
                Texture = bg,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _bgRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _bgRect.Scale = new Vector2(1.08f, 1.08f);
            AddChild(_bgRect);

            _snowFar = AddSnowLayer("SnowFar", far: true);

            _baseLeopard = ResourceLoader.Load<Texture2D>(LeopardPath);
            if (_baseLeopard == null)
            {
                Log.Info("芝士雪豹：雪豹层贴图缺失，仅显示背景板");
            }
            else
            {
                _leopardRoot = new Control { MouseFilter = MouseFilterEnum.Ignore };
                _leopardRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(_leopardRoot);

                _leopardMaterial = new ShaderMaterial { Shader = new Shader { Code = ShaderSource } };
                _leopardMaterial.SetShaderParameter("sway0", Sway0Rect);
                _leopardMaterial.SetShaderParameter("sway1", Sway1Rect);
                _leopardMaterial.SetShaderParameter("sway2", Sway2Rect);

                _leopardRect = new TextureRect
                {
                    Texture = _baseLeopard,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Material = _leopardMaterial,
                };
                _leopardRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                _leopardRoot.AddChild(_leopardRect);
            }

            _closedEyes = ResourceLoader.Load<Texture2D>(ClosedEyesPath);
            _earsVariant = ResourceLoader.Load<Texture2D>(EarsVariantPath);
            if (_closedEyes == null)
            {
                Log.Info("芝士雪豹：闭眼帧未就绪，眨眼暂不启用");
            }
            if (_earsVariant == null)
            {
                Log.Info("芝士雪豹：抖耳帧未就绪，抖耳朵暂不启用");
            }

            _snowNear = AddSnowLayer("SnowNear", far: false);

            Resized += OnResized;
            OnResized();
            StartKenBurns();
            Log.Info("芝士雪豹：动态选人背景已就绪（分层视差）");
        }

        public override void _Process(double delta)
        {
            float d = (float)delta;
            _time += delta;

            _parallax = _parallax.Lerp(MouseNormalized(), 1f - Mathf.Exp(-4f * d));

            _bgRect?.SetPosition(new Vector2(-_parallax.X * 14f, -_parallax.Y * 8f));
            if (_leopardRoot != null)
            {
                float breathe = 1f + 0.006f * Mathf.Sin((float)_time * 0.9f);
                _leopardRoot.Scale = new Vector2(1.03f * breathe, 1.03f * breathe);
                _leopardRoot.Position = new Vector2(
                    _parallax.X * 26f,
                    _parallax.Y * 14f + 4f * Mathf.Sin((float)_time * 0.9f + 0.8f));
            }

            if (_leopardRect == null)
            {
                return;
            }

            UpdateBlink(delta);
            UpdateTwitch(delta);
        }

        private Vector2 MouseNormalized()
        {
            Vector2 size = GetViewport().GetVisibleRect().Size;
            if (size == Vector2.Zero)
            {
                return Vector2.Zero;
            }
            Vector2 n = (GetViewport().GetMousePosition() / size - new Vector2(0.5f, 0.5f)) * 2f;
            return new Vector2(Mathf.Clamp(n.X, -1f, 1f), Mathf.Clamp(n.Y, -1f, 1f));
        }

        // 眨眼：直接全闭一帧（约 0.18 秒）。加新帧：往 _swapFrames 里按顺序插 (贴图, 时长) 即可。
        private void UpdateBlink(double delta)
        {
            if (_closedEyes == null || _leopardRect == null)
            {
                return;
            }

            if (_swapFrames.Count == 0)
            {
                _nextBlinkIn -= delta;
                if (_nextBlinkIn <= 0)
                {
                    _swapFrames.Add((_closedEyes, 0.18));
                    _swapFrame = 0;
                    _frameLeft = 0;
                }
                return;
            }

            _frameLeft -= delta;
            if (_frameLeft > 0)
            {
                return;
            }

            if (_swapFrame < _swapFrames.Count)
            {
                _leopardRect.Texture = _swapFrames[_swapFrame].Frame;
                _frameLeft = _swapFrames[_swapFrame].Duration;
                _swapFrame++;
            }
            else
            {
                _swapFrames.Clear();
                _leopardRect.Texture = _baseLeopard;
                _nextBlinkIn = _rng.Next(25, 60) / 10f;
            }
        }

        // 抖耳：低频触发（20~40 秒一次），双抖节奏（压耳→回正→再压耳）
        private void UpdateTwitch(double delta)
        {
            if (_earsVariant == null || _swapFrames.Count > 0)
            {
                return;
            }

            _nextTwitchIn -= delta;
            if (_nextTwitchIn <= 0)
            {
                _swapFrames.AddRange([(_earsVariant, 0.22), (_baseLeopard!, 0.12), (_earsVariant, 0.25)]);
                _swapFrame = 0;
                _frameLeft = 0;
                _nextTwitchIn = _rng.Next(200, 401) / 10f;
            }
        }

        private CpuParticles2D AddSnowLayer(string name, bool far)
        {
            var particles = new CpuParticles2D
            {
                Name = name,
                Amount = far ? 90 : 28,
                Lifetime = far ? 14f : 9f,
                Preprocess = 14f,
                Texture = MakeSoftDot(),
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                Direction = new Vector2(0f, 1f),
                Spread = 12f,
                Gravity = far ? new Vector2(5f, 18f) : new Vector2(12f, 42f),
                InitialVelocityMin = far ? 25f : 70f,
                InitialVelocityMax = far ? 55f : 130f,
                ScaleAmountMin = far ? 0.10f : 0.35f,
                ScaleAmountMax = far ? 0.22f : 0.75f,
                AngularVelocityMin = far ? -10f : -25f,
                AngularVelocityMax = far ? 10f : 25f,
                Color = new Color(1f, 1f, 1f, far ? 0.55f : 0.85f),
            };
            AddChild(particles);
            return particles;
        }

        private static Texture2D MakeSoftDot()
        {
            var gradient = new Gradient
            {
                Colors = [new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f)],
                Offsets = [0f, 1f],
            };
            return new GradientTexture2D
            {
                Gradient = gradient,
                Width = 32,
                Height = 32,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 1f),
            };
        }

        private void OnResized()
        {
            PivotOffset = Size / 2f;
            if (_bgRect != null)
            {
                _bgRect.PivotOffset = Size / 2f;
            }
            if (_leopardRoot != null)
            {
                _leopardRoot.PivotOffset = Size / 2f;
            }
            Vector2 extents = new(Size.X / 2f + 200f, 12f);
            foreach (CpuParticles2D? layer in new[] { _snowFar, _snowNear })
            {
                if (layer == null)
                {
                    continue;
                }
                layer.Position = new Vector2(Size.X / 2f, -40f);
                layer.EmissionRectExtents = extents;
            }
        }

        // 整组缓慢呼吸式缩放往返（叠加在视差与雪豹层呼吸之上）
        private void StartKenBurns()
        {
            Tween tween = CreateTween().SetLoops();
            tween.TweenProperty(this, "scale", new Vector2(1.035f, 1.035f), 12f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(this, "scale", Vector2.One, 12f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
    }

    /// <summary>
    /// 选人背景注入：CharacterSelectBg 走雪豹自己的裸 Control 场景（不碰铁甲路径），
    /// 实例化后在三个挂载点统一替换成纯代码构建的动态背景。
    /// </summary>
    [HarmonyPatch]
    internal static class SelectBg_InjectDynamicBgPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter));
            yield return AccessTools.Method(typeof(NCharacterSelectScreen), "OnLocalCharacterChangedForRandom");
            yield return AccessTools.Method(typeof(NMultiplayerLoadGameScreen), "AfterMultiplayerStarted");
        }

        private static void Postfix(object __instance)
        {
            Control? container = Traverse.Create(__instance).Field<Control>("_bgContainer").Value;
            if (container == null)
            {
                return;
            }

            string bgName = ModelDb.Character<Lingdawnpao>().Id.Entry + "_bg";
            foreach (Node child in container.GetChildren())
            {
                if (child is LingdawnpaoSelectBg || child.Name.ToString() != bgName)
                {
                    continue;
                }

                int index = child.GetIndex();
                container.RemoveChildSafely(child);
                child.QueueFreeSafely();
                var dynamicBg = new LingdawnpaoSelectBg { Name = bgName };
                container.AddChildSafely(dynamicBg);
                container.MoveChild(dynamicBg, index);
                Log.Info("芝士雪豹：已替换为动态选人背景");
            }
        }
    }
}
