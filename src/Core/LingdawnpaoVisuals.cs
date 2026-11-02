using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 战斗视觉纯代码构建：mod 不打包任何引用本体 C# 脚本的 .tscn（黑屏教训），
    /// 运行时直接 new NCreatureVisuals 并挂 SpineSprite（GDExtension 类走 ClassDB 实例化）+ 定位节点。
    /// Spine 资产为手工产物：.spatlas（atlas 文本的 JSON 包装）+ .spjson（4.2 JSON 原样副本），
    /// 由 lingdawnpao_skel_data.tres 引用（探针已验证整条加载链）。
    /// <para>
    /// ⚠️ **2026-09-23 严重事故：「进 boss 关不显示任何模型，存档重进直接黑屏」。**
    /// godot 日志坐实：同一资产在同一场游戏里连成功 3 次，第 4 次（boss 房间切换那一刻，
    /// 当时正跑在本体 `AssetLoadingSession` 的资产加载回合里）`GD.Load` 返回了 null。
    /// 而当时 <see cref="Build" /> 写的是 `return null!` —— 空视觉被当成"建造成功"塞进本体，
    /// 紧接着 `NCreature._Ready` NRE → `NCombatRoom.CreateAllyNodes` 整条中断 →
    /// **全场模型一个都没建出来**（连敌人也没有），随后的存档写进了这个半初始化状态，重进即黑屏。
    /// </para>
    /// <para>
    /// 现在的两道防线（不要再退回旧写法）：
    /// ① 骨架资源**进程级常驻**（<see cref="_skelData" />）——只在模组初始化那个干净时机加载一次并一直强引用住，
    /// 战斗路径永远读缓存，不再在房间切换时碰磁盘；
    /// ② <see cref="Build" /> 失败返回 **null**（原来是 `null!`），调用方收到 null 必须**退回本体视觉**，
    /// 最差也只是雪豹用铁甲占位形象，绝不会再把本体的模型创建链炸掉。
    /// </para>
    /// </summary>
    internal static class LingdawnpaoVisuals
    {
        private const string SkelDataPath = "res://animations/characters/lingdawnpao/v6_motion/snow_leopard_v6_skel_data.tres";

        /// <summary>骨架资源常驻引用：加载成功即缓存，避免战斗/房间切换路径反复读盘（失败不缓存，下次再试）。</summary>
        private static Resource? _skelData;

        /// <summary>模组初始化时预热（此时没有战斗、没有资产加载会话，是最干净的加载时机）。</summary>
        public static void Warmup() => _skelData ??= GD.Load<Resource>(SkelDataPath);

        // v4 source already uses a floor at y=0. Standing height is about 300 units;
        // 0.70 keeps the body near the original battle size, with space for the long tail.
        private const float SpriteScale = 0.70f;
        private static readonly Vector2 SpritePosition = Vector2.Zero;

        /// <summary>构建雪豹的 Spine 战斗视觉；**资产不可用时返回 null**（调用方必须退回本体视觉，不许塞 null 给本体）。</summary>
        public static NCreatureVisuals? Build()
        {
            Resource? skelData = _skelData ?? GD.Load<Resource>(SkelDataPath);
            if (skelData == null)
            {
                Log.Error("雪豹视觉：骨架数据加载失败 " + SkelDataPath + "，本次退回本体占位形象（不再连锁破坏战斗场景）");
                return null;
            }

            _skelData = skelData;

            var visuals = new NCreatureVisuals { Name = "LingdawnpaoVisuals" };

            var sprite = ClassDB.Instantiate("SpineSprite").As<Node2D>();
            sprite.Name = "Visuals";
            sprite.Set("skeleton_data_res", Variant.From(skelData));
            sprite.Position = SpritePosition;
            sprite.Scale = new Vector2(SpriteScale, SpriteScale);
            AddOwned(visuals, sprite);

            var bounds = new Control
            {
                Name = "Bounds",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                OffsetLeft = -245f,
                OffsetTop = -225f,
                OffsetRight = 210f,
                OffsetBottom = 0f,
            };
            AddOwned(visuals, bounds);

            AddOwned(visuals, new Marker2D { Name = "IntentPos", Position = new Vector2(0f, -255f) });
            AddOwned(visuals, new Marker2D { Name = "CenterPos", Position = new Vector2(0f, -120f) });
            AddOwned(visuals, new Marker2D { Name = "OrbPos", Position = new Vector2(0f, -236f) });
            AddOwned(visuals, new Marker2D { Name = "TalkPos", Position = new Vector2(140f, -200f) });

            // ⚠️ **本体 public-beta 新增的必需节点**（2026-09-25 用户报障"放弃游戏时人死了但没退出、还能继续打"）：
            // beta 的死亡动画会走 `NCreatureVisuals.RemoveFormVfx()`，而它在 `FormVfxHolder`（= `_Ready` 里
            // `GetNodeOrNull<Control>("%FormVfx")`）为 null 时直接
            // `throw new InvalidOperationException("This creature has no form holder to put this form vfx")`。
            // 我们这套视觉是**代码手搭**的（不是本体场景），本来没有这个节点 → 异常从
            // `RunManager.AbandonInternal → GuaranteeKillAllPlayers → CreatureCmd.Kill → StartDeathAnim`
            // 一路抛出去，把"弃局/死亡"的收尾流程打断，于是出现"死了但没退出"。
            // 补一个空 Control 即可：有 holder 时 `FreeChildren()` 是空操作，平时不画任何东西。
            AddOwned(visuals, new Control
            {
                Name = "FormVfx",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            Log.Info("雪豹视觉：已构建 Spine 战斗视觉（scale=" + SpriteScale + "）");
            return visuals;
        }

        // % 唯一名解析要求 Owner 指向 visuals 根（NCreatureVisuals._Ready 用 %Visuals/%Bounds/... 绑定）
        private static void AddOwned(Node parent, Node child)
        {
            parent.AddChild(child);
            child.Owner = parent;
            child.UniqueNameInOwner = true;
        }
    }

    /// <summary>战斗/结算主路径：Creature.CreateVisuals 方法体够大，无 JIT 内联风险。</summary>
    [HarmonyPatch(typeof(Creature), nameof(Creature.CreateVisuals))]
    internal static class Creature_CreateVisualsPatch
    {
        private static bool Prefix(Creature __instance, ref NCreatureVisuals? __result)
        {
            if (TestMode.IsOn || __instance.Player?.Character is not Lingdawnpao)
            {
                return true;
            }

            NCreatureVisuals? visuals = LingdawnpaoVisuals.Build();
            if (visuals == null)
            {
                return true; // 资产不可用 → 退回本体视觉（VisualsPath 指向铁甲占位），绝不能把 null 塞给本体
            }

            __result = visuals;
            return false;
        }
    }

    /// <summary>
    /// 兜底路径：NFakeMerchant / NUnlockCharacterScreen 直接调 CharacterModel.CreateVisuals。
    /// 该方法只有一行、非虚，可能被 JIT 内联导致补丁不生效（TryManualPlay 教训）——
    /// 内联时这些界面退回铁甲占位形象（VisualsPath 重定向），不影响战斗。
    /// </summary>
    [HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CreateVisuals))]
    internal static class CharacterModel_CreateVisualsPatch
    {
        private static bool Prefix(CharacterModel __instance, ref NCreatureVisuals __result)
        {
            if (TestMode.IsOn || __instance is not Lingdawnpao)
            {
                return true;
            }

            NCreatureVisuals? visuals = LingdawnpaoVisuals.Build();
            if (visuals == null)
            {
                return true; // 同 Creature 路径：资产不可用就退回本体，别把 null 交给本体
            }

            __result = visuals;
            return false;
        }
    }
}
