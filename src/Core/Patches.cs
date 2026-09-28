using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using lingdawnpao.Core.Relics;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 把芝士雪豹加进角色列表。ModelDb.AllCharacters 是硬编码的 5 角色数组
    /// （属性每次调用都新建数组，无缓存，因此 Postfix 追加即可），角色选单等 UI 都遍历它。
    /// </summary>
    internal static class AmbushBury
    {
        public static bool ShouldBury(CardModel card, Creature? target, out Player? owner)
        {
            owner = card.Owner;
            // 必须真的有伏击者：伏击形态是 mod 自己的静态状态，换角色后可能还留着 true，
            // 没有遗物就没人把牌带出来，牌会永久卡在区域里
            if (owner?.PlayerCombatState == null || owner.GetRelic<FrostFang>() == null)
            {
                return false;
            }

            // 冷却中（过载的代价 / 凝神 / 背水一战，`IsCoolingDown`）**绝不能拦**：BuryAsync 一进来见到冷却就
            // 直接 return，拦下来就等于"牌被压进屏幕中央、既回不了手也打不出"（2026-09-22 用户报障）。
            // 区域失效时这张牌本来就该按普通出牌结算。
            //
            // ⚠️ **每一处拦截都要留下理由**：这段是纯本地预判，静默返回会把"埋不了牌"变成无头案——
            // 2026-09-23 用户报障"伏击区没进冷却却不能埋牌"，当时一条日志都没有，只能靠猜。
            // 本方法只在**伏击形态下打出牌**时被调用（调用方先判 IsAmbushMode），所以不会刷屏。
            AmbushZoneState zone = AmbushZone.For(owner);
            if (zone.IsCoolingDown)
            {
                Log.Info("伏击者：不埋牌——伏击区处于冷却");
                return false;
            }

            if (zone.IsFull)
            {
                Log.Info($"伏击者：不埋牌——伏击区已满（{zone.Count}/{zone.Capacity}）");
                return false;
            }

            if (!card.CanPlayTargeting(target))
            {
                Log.Info($"伏击者：不埋牌——{card.Id.Entry} 当前无法对指定目标打出");
                return false;
            }

            int cost = card.EnergyCost.GetAmountToSpend();
            if (cost > owner.PlayerCombatState.Energy)
            {
                Log.Info($"伏击者：不埋牌——能量不足（需要 {cost}，当前 {owner.PlayerCombatState.Energy}）");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 埋卡主拦截点：NCardPlay.TryPlayCard（鼠标/手柄出牌的共同入口）。
    /// 不能拦 CardModel.TryManualPlay——它只有几行，会被 CoreCLR JIT 内联进调用方，
    /// 补丁 detour 直接被绕过（实测 Prefix 从未执行）。
    /// 这里不就地改任何状态，只把 BuryCardAction 丢进同步队列（本体出牌就是这么做的），
    /// 队列会保证每个客户端按同一顺序执行 → 联机不脱同步。
    /// 随后调 Cleanup(true) 走"出牌成功"的节点清理路径。
    /// </summary>
    [HarmonyPatch]
    internal static class NCardPlay_TryPlayCard_AmbushBuryPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(NCardPlay_TryPlayCard_AmbushBuryPatch));
        private static readonly MethodInfo CardGetter = AccessTools.PropertyGetter(typeof(NCardPlay), "Card");

        private static readonly MethodInfo CleanupMethod = AccessTools.Method(typeof(NCardPlay), "Cleanup");

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(NCardPlay), "TryPlayCard");

        private static bool Prefix(NCardPlay __instance, Creature? target)
        {
            CardModel? card = (CardModel?)CardGetter.Invoke(__instance, null);
            Player? owner = card?.Owner;
            if (card == null || owner == null || !AmbushZone.For(owner).IsAmbushMode
                || !AmbushBury.ShouldBury(card, target, out owner))
            {
                return true;
            }

            Log.Info($"伏击者：伏击形态，拦截出牌 {card.Id.Entry} → 入队埋牌动作");
            PendingBuryVisual.Register(__instance); // 记住 holder：埋牌若在队列里被跳过，把卡面还回手牌
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new BuryCardAction(card));
            CleanupMethod.Invoke(__instance, new object[] { true });
            return false;
        }
    }

    /// <summary>
    /// 卡面节点池复用复位补丁（对"透明卡/幽灵卡"bug 的釜底抽薪修复，2026-09-28）：
    /// <para>
    /// <see cref="NCard.OnReturnedFromPool" /> 内部有 <c>IsNodeReady()</c> 闸门——而归池的节点
    /// 都是被 QueueFreeSafely **先移出场景树**再延迟归池的，取出瞬间节点仍不在树内，闸门为 false，
    /// 内部复位（透明度/缩放/可见性/绑定清理）**整体跳过**。于是下一个从池里拿到该节点的界面
    /// （手牌、卡牌奖励、查看堆）原样继承上一张卡渐隐到一半的 modulate 与缩放
    /// ——就是用户看到的"透明卡/黑卡，卡位还在还能打出"。
    /// </para>
    /// <para>
    /// 本补丁在每次取出后**强制补复位**最关键的视觉字段；取用方（NCard.Create 等）随后会
    /// 重设 Model 与布局，不存在冲突。纯视觉层修补，不改任何游戏状态。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(NCard), nameof(NCard.OnReturnedFromPool))]
    internal static class NCard_OnReturnedFromPool_ForceResetPatch
    {
        private static void Postfix(NCard __instance)
        {
            __instance.Modulate = Colors.White;
            __instance.Visible = true;
            __instance.Scale = Vector2.One;
            __instance.Rotation = 0f;
            __instance.Position = Vector2.Zero;
        }
    }

    /// <summary>
    /// 兜底拦截：CardModel.TryManualPlay（若未被 JIT 内联则会生效；TryPlayCard 拦截点先于它，
    /// 两处不会重复埋）。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.TryManualPlay))]
    internal static class TryManualPlay_AmbushBuryPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(TryManualPlay_AmbushBuryPatch));
        private static bool Prefix(CardModel __instance, Creature? target, ref bool __result)
        {
            Player? owner = __instance.Owner;
            if (owner == null || !AmbushZone.For(owner).IsAmbushMode
                || !AmbushBury.ShouldBury(__instance, target, out owner))
            {
                return true;
            }

            __result = true;
            Log.Info($"伏击者：伏击形态，拦截出牌 {__instance.Id.Entry} → 入队埋牌动作（TryManualPlay 路径）");
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new BuryCardAction(__instance));
            return false;
        }
    }

    /// <summary>
    /// 遗物右键：NClickableControl._GuiInput 对左/右键都会发 MousePressed/MouseReleased，
    /// 但 MouseReleased 是松开事件（Pressed=false），因此订阅 MousePressed 判断按下+右键。
    /// </summary>
    [HarmonyPatch(typeof(NRelicInventoryHolder), nameof(NRelicInventoryHolder.Create), new[] { typeof(RelicModel) })]
    internal static class NRelicInventoryHolder_Create_RightClickPatch
    {
        private static void Postfix(NRelicInventoryHolder? __result, RelicModel relic)
        {
            if (__result == null || relic is not FrostFang fang)
            {
                return;
            }

            Log.Info("伏击者：遗物图标已挂接右键监听");
            __result.MousePressed += inputEvent =>
            {
                if (inputEvent is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                {
                    Log.Info("伏击者：检测到右键");
                    fang.ToggleAmbushMode();
                }
            };
        }
    }

    /// <summary>
    /// 把芝士雪豹加进角色列表。ModelDb.AllCharacters 是硬编码的 5 角色数组
    /// （属性每次调用都新建数组，无缓存，因此 Postfix 追加即可），角色选单等 UI 都遍历它。
    /// </summary>
    [HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllCharacters), MethodType.Getter)]
    internal static class ModelDb_AllCharacters_AddLingdawnpaoPatch
    {
        private static void Postfix(ref IEnumerable<CharacterModel> __result)
        {
            __result = __result.Append(ModelDb.Character<Lingdawnpao>());
        }
    }

    /// <summary>
    /// 音频占位：角色专属 FMOD 事件（event:/sfx/characters/lingdawnpao/...）不存在，
    /// 缺失事件会抛 InvalidOperationException 中断调用方（选人界面点了没反应就是它）。
    /// 统一重定向到铁甲战士的音频事件。
    /// </summary>
    [HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot), new[] { typeof(string), typeof(float) })]
    internal static class NAudioManager_PlayOneShot_RedirectPatch
    {
        private static void Prefix(ref string path)
        {
            path = SfxRedirect.Redirect(path);
        }
    }

    [HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot), new[] { typeof(string), typeof(Dictionary<string, float>), typeof(float) })]
    internal static class NAudioManager_PlayOneShotWithParams_RedirectPatch
    {
        private static void Prefix(ref string path)
        {
            path = SfxRedirect.Redirect(path);
        }
    }

    internal static class SfxRedirect
    {
        // 整体替换：目录段（characters/lingdawnpao/）和文件名（wipe_lingdawnpao）里的角色 ID 都要换成铁甲的
        public static string Redirect(string path) => path?.Replace("lingdawnpao", "ironclad") ?? path!;
    }

    /// <summary>
    /// 自制角色打赢**精英**时的"成就检查"兜底。
    /// 本体的 `ProgressSaveManager.CheckFifteenElitesDefeatedEpoch` 用一串**写死的角色判断**
    /// （Ironclad / Silent / Regent / Defect / Necrobinder / Deprived）去取对应 epoch，
    /// 落到任何别的 CharacterModel 上就 `throw new ArgumentOutOfRangeException("character", character, null)`。
    /// **后果非常严重**：这个异常发生在 `CombatManager.EndCombatInternal` 的异步链上
    /// （被 `TaskHelper.LogTaskExceptions` 吞掉），于是**整场战斗的收尾中断** ——
    /// 奖励界面永远不会被创建，玩家看到的就是"打赢精英后卡死、没有任何牌可以选"。
    /// 2026-09-23 用户实测复现（encounter = `BYGONE_EFFIGY_ELITE`，异常原文
    /// `Actual value was CHARACTER.LINGDAWNPAO (8812249)`，堆栈见 godot.log）。
    /// **处置：只对我们的角色跳过该方法**（前缀返回 false）。那两个 epoch 是本体的
    /// 角色专属成就，自制角色本来就不该往里写；作用域刻意锁死在 `Lingdawnpao`，
    /// 不改变本体角色、也不改变其它 mod 的角色（它们各自会踩同一个 throw，那是它们自己的事）。
    /// ⚠️ **姊妹方法是 `CheckFifteenBossesDefeatedEpoch`（15 个 boss），同样的写死链条、同样的崩溃，
    /// 两个都要补**——这就是"精英与 boss 战后都卡死"的原因。
    /// </summary>
    [HarmonyPatch(typeof(ProgressSaveManager), "CheckFifteenElitesDefeatedEpoch")]
    internal static class ProgressSaveManager_EliteEpoch_LingdawnpaoGuardPatch
    {
        private static bool Prefix(Player localPlayer) => localPlayer.Character is not Lingdawnpao;
    }

    /// <summary>见上：`CheckFifteenBossesDefeatedEpoch` 是同款写死链条，boss 战后卡死就是它。</summary>
    [HarmonyPatch(typeof(ProgressSaveManager), "CheckFifteenBossesDefeatedEpoch")]
    internal static class ProgressSaveManager_BossEpoch_LingdawnpaoGuardPatch
    {
        private static bool Prefix(Player localPlayer) => localPlayer.Character is not Lingdawnpao;
    }

    /// <summary>
    /// 场景占位：CharacterModel 的场景路径按角色 ID 拼接，但复制的 .tscn 经官方 Godot 导出转二进制时
    /// 引用的本体 C# 脚本无法解析（烘焙出空类名，运行时实例化为裸节点 → 战斗黑屏）。
    /// 因此不往 mod 打包任何场景，直接把芝士雪豹的场景路径全部指到本体铁甲战士的场景。
    /// </summary>
    [HarmonyPatch(typeof(CharacterModel))]
    internal static class CharacterModel_IroncladScenePathPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // CharacterSelectBg 故意不在列表里：芝士雪豹用自己的裸 Control 场景
            //（scenes/screens/char_select/char_select_bg_lingdawnpao.tscn，无脚本引用所以导出安全），
            // 运行时由 SelectBg_InjectDynamicBgPatch 换成纯代码构建的动态背景
            foreach (string name in new[]
            {
                "VisualsPath", "EnergyCounterPath", "MerchantAnimPath", "RestSitePath",
                "RestSiteAnimPath", "TrailPath", "CharacterSelectTransitionPath",
            })
            {
                MethodBase getter = AccessTools.PropertyGetter(typeof(CharacterModel), name);
                if (getter != null)
                {
                    yield return getter;
                }
            }
        }

        private static void Postfix(MethodBase __originalMethod, CharacterModel __instance, ref string __result)
        {
            if (__instance is not Lingdawnpao)
            {
                return;
            }

            __result = __originalMethod.Name switch
            {
                "get_VisualsPath" => SceneHelper.GetScenePath("creature_visuals/ironclad"),
                "get_EnergyCounterPath" => SceneHelper.GetScenePath("combat/energy_counters/ironclad_energy_counter"),
                "get_MerchantAnimPath" => SceneHelper.GetScenePath("merchant/characters/ironclad_merchant"),
                "get_RestSiteAnimPath" or "get_RestSitePath" => SceneHelper.GetScenePath("rest_site/characters/ironclad_rest_site"),
                "get_TrailPath" => SceneHelper.GetScenePath("vfx/card_trail_ironclad"),
                "get_CharacterSelectTransitionPath" => "res://materials/transitions/ironclad_transition_mat.tres",
                _ => __result,
            };
        }
    }

    /// <summary>
    /// 把芝士雪豹的战斗能量计数器换成自制雪花（纯代码构建，见 LingdawnpaoEnergyCounter）。
    /// 后置替换：雪花图缺失 / 构建异常时 TryCreate 原样返回本体结果，游戏不受影响。
    /// </summary>
    [HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter.Create))]
    internal static class NEnergyCounter_Create_LingdawnpaoOrbPatch
    {
        private static void Postfix(Player player, ref NEnergyCounter? __result)
        {
            __result = LingdawnpaoEnergyCounter.TryCreate(player, __result);
        }
    }

    /// <summary>
    /// 判定一张卡是否属于芝士雪豹（该角色卡）。自有卡实现 ILingdawnpaoCard 标记接口；
    /// 事件等途径获得的其他角色卡（含本体的打击/防御）不命中，保持各自原样（原版配色规则）。
    /// </summary>
    internal static class LingdawnpaoOwnCards
    {
        public static bool IsOwn(CardModel card) => card is ILingdawnpaoCard;
    }

    /// <summary>
    /// 卡牌左上角费用图标换成自制雪花。仅雪豹角色卡替换；
    /// run 外（卡牌库）与其他角色卡保持原版图标。
    /// ⚠️ 必须再校验一次贴图有效性：本体给的 `__result` 走的是自己的资源查找，
    /// 而我们是长期持有静态引用的那一方。贴图若已被释放而我们照样返回，
    /// 本体 `NCard.Reload` 里的 `SetTexture` 会抛 ObjectDisposedException 并**中断整个 Reload**
    /// （卡面停在上一张的类型标签与文案上，2026-09-22 用户看到的「防御 = Attack」即此）。
    /// 失效时直接不替换，退回本体图标，宁可少一个装饰也不能让卡面渲染挂掉。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.EnergyIcon), MethodType.Getter)]
    internal static class CardModel_EnergyIcon_LingdawnpaoSnowflakePatch
    {
        private static void Postfix(CardModel __instance, ref Texture2D __result)
        {
            if (RunManager.Instance?.GetLocalCharacterEnergyIconPrefix()
                    == EnergyIconHelper_GetPrefix_LingdawnpaoPatch.Prefix
                && LingdawnpaoOwnCards.IsOwn(__instance)
                && LingdawnpaoEnergyCounter.Texture is { } texture
                && GodotObject.IsInstanceValid(texture))
            {
                __result = texture;
            }
        }
    }

    /// <summary>
    /// 所有文字描述里内嵌的能量小图标前缀（卡牌/能力/遗物/药水描述、能量提示框）：
    /// 原版按 EnergyColorName 拼 res://images/packed/sprite_fonts/{prefix}_energy_icon.png，
    /// 借用 defect 时显示蓝球。芝士雪豹语境下改成自定义前缀 lingdawnpao，
    /// 对应 mod 内 24px 小图 lingdawnpao_energy_icon.png；
    /// 能量提示框头部图标走 GetPath(prefix)，对应 mod 内 energy_lingdawnpao.tres。
    /// </summary>
    [HarmonyPatch(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPrefix),
        new[] { typeof(AbstractModel) })]
    internal static class EnergyIconHelper_GetPrefix_LingdawnpaoPatch
    {
        public const string Prefix = "lingdawnpao";

        private static void Postfix(AbstractModel model, ref string __result)
        {
            if (__result == Prefix || !IsLingdawnpaoContext(model))
            {
                return;
            }
            __result = Prefix;
        }

        private static bool IsLingdawnpaoContext(AbstractModel model)
        {
            switch (model)
            {
                case LingdawnpaoCardPool:
                case LingdawnpaoRelicPool:
                    return true;
                case CardModel card:
                    // 仅雪豹角色卡；事件等途径获得的其他角色卡保持其原生图标
                    return LingdawnpaoOwnCards.IsOwn(card);
                case RelicModel:
                case PotionModel:
                    // 雪豹 run 内：遗物/药水描述的能量图标统一雪花（不依赖 Owner 链，
                    // 避免 cloned 模型 owner 未就绪时漏判）
                    return RunManager.Instance?.GetLocalCharacterEnergyIconPrefix() == Prefix;
                case PowerModel power when power.IsMutable && power.Owner is { IsPlayer: true }:
                    return power.Owner.Player!.Character is Lingdawnpao;
                case EnchantmentModel enchantment when enchantment.HasCard:
                    return enchantment.Card.Owner?.Character is Lingdawnpao;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// 雪豹角色卡的浅冰蓝框材质（与雪花能量同色系）。
    /// 注意：不能只补丁 CardModel.FrameMaterial getter —— 它只有一行
    /// （=&gt; VisualCardPool.FrameMaterial），会被 CoreCLR JIT 内联进 NCard.Reload，
    /// detour 被绕过；NCard 又是对象池复用节点，会保留上一次的红材质。
    /// 因此材质在此代码构建（直接加载本体 hsv shader + 参数），
    /// 由下面的 NCard.Reload / NTinyCard.SetCard 后置补丁直接赋值到框节点。
    /// </summary>
    internal static class LingdawnpaoFrameMaterial
    {
        public static Material? Get()
        {
            if (_material != null)
            {
                return _material;
            }
            var shader = ResourceLoader.Load<Shader>("res://shaders/hsv.gdshader");
            if (shader == null)
            {
                return null;
            }
            _material = new ShaderMaterial { Shader = shader };
            _material.SetShaderParameter("h", 0.535f);
            _material.SetShaderParameter("s", 0.38f);
            _material.SetShaderParameter("v", 1.0f);
            return _material;
        }

        private static ShaderMaterial? _material;

        public static bool IsActiveFor(CardModel card) =>
            RunManager.Instance?.GetLocalCharacterEnergyIconPrefix()
                == EnergyIconHelper_GetPrefix_LingdawnpaoPatch.Prefix
            && LingdawnpaoOwnCards.IsOwn(card);
    }

    /// <summary>
    /// 卡框主注入点：NCard.Reload（大方法不会被内联）。后置把 _frame 的材质
    /// 直接换成浅冰蓝；外来角色卡不命中 IsOwn，保持原色。
    /// </summary>
    [HarmonyPatch(typeof(NCard), "Reload")]
    internal static class NCard_Reload_LingdawnpaoFramePatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(NCard_Reload_LingdawnpaoFramePatch));
        private static readonly FieldInfo FrameField =
            AccessTools.Field(typeof(NCard), "_frame");

        private static void Postfix(NCard __instance)
        {
            CardModel? model = __instance.Model;
            if (model == null || !LingdawnpaoFrameMaterial.IsActiveFor(model)
                || LingdawnpaoFrameMaterial.Get() is not { } material)
            {
                return;
            }
            if (FrameField.GetValue(__instance) is TextureRect frame)
            {
                frame.Material = material;
            }
        }
    }

    /// <summary>
    /// 小卡（run history 等缩略卡）同样在 SetCard 后直接替换 _cardBack 材质。
    /// </summary>
    [HarmonyPatch(typeof(NTinyCard), nameof(NTinyCard.SetCard),
        new[] { typeof(CardModel) })]
    internal static class NTinyCard_SetCard_LingdawnpaoFramePatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(NTinyCard_SetCard_LingdawnpaoFramePatch));
        private static readonly FieldInfo CardBackField =
            AccessTools.Field(typeof(NTinyCard), "_cardBack");

        private static void Postfix(NTinyCard __instance, CardModel card)
        {
            if (!LingdawnpaoFrameMaterial.IsActiveFor(card)
                || LingdawnpaoFrameMaterial.Get() is not { } material)
            {
                return;
            }
            if (CardBackField.GetValue(__instance) is TextureRect cardBack)
            {
                cardBack.Material = material;
            }
        }
    }

    /// <summary>
    /// **就绪的埋伏卡亮金框**（2026-09-23 用户定稿的伏击堆可读性方案）。
    /// <para>
    /// 规则：伏击区域里**带「埋伏」词条、且已潜伏满一回合**的牌 → 亮**金色**高亮；
    /// 其余一律**正常显示**（**不再有降暗**——用户报障"所有卡进伏击区都变暗"，
    /// 那是上一版<未就绪降暗>的实现，会把不相关的能力牌/事件牌也一起压暗，已整体移除）。
    /// </para>
    /// <para>
    /// **两个挂点缺一不可**（⚠️ 2026-09-23 用户报障"能触发埋伏的牌在伏击区里没有金框"，就是只挂了一个）：
    /// 本体 `NCardGrid` 有两条互相独立的刷牌路径，各自都不会调对方——
    /// </para>
    /// <list type="bullet">
    /// <item><b>`NCardGrid.InitGrid`</b>：**建界面**这条路（打开伏击堆界面走
    /// `NCardPileScreen.OnPileContentsChanged → SetCards → InitGrid`）。它逐个 `NCard.Create`
    /// ＋`UpdateVisuals` 建好 holder，**从不调 `AssignCardsToRow`**。</item>
    /// <item><b>`NCardGrid.AssignCardsToRow`</b>：**滚动重排**这条路（只由
    /// `ReallocateAll/Above/Below` 调，而它们只在 `UpdateScrollPosition` 里滚动位置变了才跑）。
    /// 它会给每张牌按 `_highlightedCards` **强制** `AnimHide()`（伏击堆界面从不调 `HighlightCard`，
    /// 所以那些卡一律被隐藏），后置补丁必须跑在这个 AnimHide 之后才能把它盖掉。</item>
    /// </list>
    /// <para>
    /// 只挂 `AssignCardsToRow` 的后果：界面一打开是"从未施加过金框"的状态，**玩家不滚动就永远看不到**；
    /// 滚动一下才亮起来。两条路都挂上，实际施加逻辑共用 <see cref="AmbushReadyGlow" />，不会不一致。
    /// </para>
    /// <para>
    /// ⚠️ **必须每次重放 `AnimShow()` 而不是直接改 shader 参数**：`AnimHide()` 会起一条 0.5 秒的
    /// tween 持续写 shader 的 `width`，滚动跨行时 `AssignCardsToRow` 会被反复调用。
    /// `AnimShow()` 内部先 `Kill` 掉当前 tween 再起自己的，所以"补丁最近一次调用"总能赢；
    /// 直接设参数则会被那条还活着的 hide tween 每帧覆盖掉（表现为金框闪一下就灭）。
    /// </para>
    /// <para>
    /// 用本体 `NCardHighlight.gold` 而不是自绘边框：这就是「霜华迸发」等卡用的同一套金色发光特效，
    /// 玩家已经在别的语境里认识它。
    /// </para>
    /// </summary>
    internal static class AmbushReadyGlow
    {
        /// <summary>把"就绪埋伏卡亮金框"施加到给定的 holder 上（两个挂点共用的唯一实现），返回点亮张数。</summary>
        public static int Apply(IEnumerable<NGridCardHolder> holders)
        {
            int lit = 0;
            try
            {
                foreach (NGridCardHolder holder in holders)
                {
                    if (holder == null || !GodotObject.IsInstanceValid(holder) || !holder.Visible)
                    {
                        continue;
                    }

                    NCard? node = holder.CardNode;
                    if (node == null || !GodotObject.IsInstanceValid(node) || node.Model == null)
                    {
                        continue;
                    }

                    // CardHighlight 也可能未初始化（青蛙骑士战斗中某张牌的 NCard 状态异常时报障
                    // "模型消失 + 黑屏"，怀疑本路径 NRE 触发——见 PatchSwallower 的设计说明）
                    var highlight = node.CardHighlight;
                    if (highlight == null || !GodotObject.IsInstanceValid(highlight))
                    {
                        continue;
                    }

                    if (!AmbushZone.IsReadyForAmbush(node.Model))
                    {
                        continue;
                    }

                    highlight.Modulate = NCardHighlight.gold;
                    highlight.AnimShow();
                    lit++;
                }
            }
            catch (System.Exception e)
            {
                Log.Warn($"芝士雪豹：伏击堆金框刷新抛 {e.GetType().Name}: {e.Message}，已跳过本次刷新");
            }

            return lit;
        }
    }

    /// <summary>建界面路径的金框（详见 <see cref="AmbushReadyGlow" /> 的说明）。</summary>
    /// <remarks>
    /// ⚠️ `InitGrid` 有**两个重载**（无参的 `protected virtual void InitGrid()` 与私有的
    /// `async Task InitGrid(Task?)`），所以必须显式给出参数类型表 `new System.Type[0]`：
    /// 只写方法名会让 `AccessTools.Method` 撞上 `AmbiguousMatchException`，整个补丁类被
    /// `PatchAllTolerant` 跳过（症状与"没挂"一样，但日志里有应用失败记录）。
    /// </remarks>
    [HarmonyPatch(typeof(NCardGrid), "InitGrid", new System.Type[0])]
    internal static class NCardGrid_InitGrid_AmbushReadyGlowPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(NCardGrid_InitGrid_AmbushReadyGlowPatch));

        private static void Postfix(NCardGrid __instance)
        {
            int lit = AmbushReadyGlow.Apply(__instance.CurrentlyDisplayedCardHolders);
            if (lit > 0)
            {
                // 只在真的点亮了才留一行（伏击堆界面一次打开一行，抽牌/弃牌堆不会命中，不刷屏）
                Log.Info($"伏击堆：界面已建好，{lit} 张就绪埋伏卡亮金框");
            }
        }
    }

    /// <summary>滚动重排路径的金框（详见 <see cref="AmbushReadyGlow" /> 的说明）。</summary>
    // AssignCardsToRow 是 protected virtual，跨程序集不能用 nameof（可访问性限制），故写字符串
    [HarmonyPatch(typeof(NCardGrid), "AssignCardsToRow")]
    internal static class NCardGrid_AssignCardsToRow_AmbushReadyGlowPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(NCardGrid_AssignCardsToRow_AmbushReadyGlowPatch));

        private static void Postfix(List<NGridCardHolder> row) => AmbushReadyGlow.Apply(row);
    }

    /// <summary>
    /// 联机面板里的能量小图（CardPoolModel.EnergyIconPath，直接用 EnergyColorName）：
    /// LingdawnpaoCardPool 也换成雪花路径，与各处保持一致。
    /// </summary>
    [HarmonyPatch(typeof(CardPoolModel), "get_EnergyIconPath")]
    internal static class CardPoolModel_EnergyIconPath_LingdawnpaoSnowflakePatch
    {
        private static void Postfix(CardPoolModel __instance, ref string __result)
        {
            if (__instance is LingdawnpaoCardPool
                && ResourceLoader.Exists(LingdawnpaoEnergyCounter.SnowflakePath))
            {
                __result = LingdawnpaoEnergyCounter.SnowflakePath;
            }
        }
    }

    /// <summary>
    /// 狩猎形态的「你的攻击无视格挡」。
    /// 挂在本体伤害的公共入口 CreatureCmd.Damage 的 6 参重载上——卡牌攻击（AttackCommand.Execute）
    /// 与大部分伤害都汇到这里，且它是大方法，符合 K2 的"别挂单行小方法，会被 JIT 内联绕过"。
    /// 只在「伤害来源是玩家 + 该玩家身上有狩猎形态 + 这次是攻击伤害（Move 且非 Unpowered）」时
    /// 叠加 Unblockable；下游 Creature.DamageBlockInternal 就是靠这个 flag 决定要不要扣格挡，
    /// 所以只改 props 即可，不用碰伤害计算本身。
    /// </summary>
    [HarmonyPatch]
    internal static class CreatureCmd_Damage_CalamityFormPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(CreatureCmd_Damage_CalamityFormPatch));
        /// <summary>
        /// 群体伤害重载的两种形态：public 是 6 参
        /// `(ctx, IEnumerable&lt;Creature&gt;, decimal, ValueProp, Creature, CardModel)`；
        /// public-beta 末尾多了 `CardPlay?` 变成 7 参，**6 参版已经不存在**。
        /// ⚠️ 2026-09-25 实测事故：这里原先只按 6 参精确签名找，在 beta 上拿到 null →
        /// 补丁静默失效（狩猎形态的"攻击无视格挡"整个丢掉，不崩但机制没了）。
        /// 现在两种都探、用存在的那个；前缀只声明 `props` / `dealer`，两种形态都能挂。
        /// 解析结果同时给启动自检用（见 <see cref="ModInitializer" /> 的关键项）。
        /// </summary>
        internal static MethodInfo? TargetOrNull() =>
            AccessTools.Method(typeof(CreatureCmd), "Damage", new[]
            {
                typeof(PlayerChoiceContext),
                typeof(IEnumerable<Creature>),
                typeof(decimal),
                typeof(ValueProp),
                typeof(Creature),
                typeof(CardModel),
            })
            ?? AccessTools.Method(typeof(CreatureCmd), "Damage", new[]
            {
                typeof(PlayerChoiceContext),
                typeof(IEnumerable<Creature>),
                typeof(decimal),
                typeof(ValueProp),
                typeof(Creature),
                typeof(CardModel),
                typeof(CardPlay),
            });

        private static MethodBase TargetMethod() => TargetOrNull()!;

        private static void Prefix(ref ValueProp props, Creature? dealer)
        {
            if (dealer?.Player == null || !props.IsPoweredAttack()
                || dealer.GetPower<CalamityFormPower>() == null)
            {
                return;
            }

            props |= ValueProp.Unblockable;
        }
    }

    /// <summary>
    /// 「草丛」（遗物）：从[gold]伏击区域[/gold]带出的攻击牌伤害+2——2026-09-27 用户定稿。
    /// <para>
    /// 挂点选 <c>CreatureCmd.Damage</c> 的**核心重载**（带 targets/amount/cardSource 的那个，
    /// 狩猎形态补丁同款位置）：所有卡牌伤害的必经收口。筛选条件与「十面埋伏」的记账同口径：
    /// ① <see cref="AmbushZone.IsAmbushPlay" />（这张伤害来自带出的伏击牌——跨回合潜伏过的）；
    /// ② 只吃[gold]攻击[/gold]伤害（IsPoweredAttack，撕裂/挂冰等 Unpowered 不吃）；
    /// ③ 施加者带草丛。多段攻击每段各+2（照"伤害+2"的口径）。
    /// 加法落在 CreatureCmd.Damage 入口 = 乘区（破绽等）**之前**，与本体"先加法、再乘倍率"的口径一致。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class CreatureCmd_Damage_ThicketPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // 只认核心重载：签名同时含 targets / amount / cardSource（外层包装重载是 target 单数或 damageVar）
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
                         .Where(m => m.Name == "Damage"))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Any(p => p.Name == "targets")
                    && parameters.Any(p => p.Name == "amount")
                    && parameters.Any(p => p.Name == "cardSource"))
                {
                    yield return method;
                }
            }
        }

        private static void Prefix(ref decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        {
            if (dealer?.Player == null || cardSource == null || !props.IsPoweredAttack()
                || !AmbushZone.IsAmbushPlay(cardSource)
                || dealer.Player.GetRelic<Thicket>() == null)
            {
                return;
            }

            amount += 2m;
        }
    }

    /// <summary>
    /// 伤害乘区（破绽 / 听桥 / 追猎）的**跨分支挂点**——2026-09-25 单 DLL 双分支适配。
    /// <para>
    /// 为什么不再用 override：`AbstractModel.ModifyDamageMultiplicative` 在 public-beta 末尾多了一个
    /// `CardPlay?`（public 5 参 / beta 6 参），而 override 必须逐字匹配签名，一份 DLL 没法同时匹配。
    /// 改挂**基类的虚拟实现**：我们那三个 Power 都不再 override，于是本体伤害钩子链调到它们身上时
    /// 落进基类实现（返回 1m），被这里的 Postfix 截住、按 `__instance` 类型分发。
    /// </para>
    /// <para>
    /// 两个刻意的取舍：
    /// ① **不挪到 `CreatureCmd.Damage` 入口去做**：本体那里是"先加力量等加法、再乘倍率"，
    ///    挪到入口会把倍率乘在加法**之前**（×(基础×倍率+力量) ≠ (基础+力量)×倍率，力量越高差得越多），
    ///    挂在下面这个方法上取值时机才与原来的 override 完全一致。
    /// ② Postfix 只声明两个分支都存在的参数名（`target` / `props` / `cardSource`），beta 多出来的
    ///    `cardPlay` 不声明即可——Harmony 按名字注入，未声明的原方法参数会被忽略。
    /// </para>
    /// <para>
    /// 副作用边界：基类实现是所有**没有自己 override 这个方法**的模型（含本体与其它 mod 的 Power）的公共落点，
    /// 所以这个 Postfix 会替它们也跑一遍；不是我们那三个 Power 时直接返回、不碰 `__result`，行为与打补丁前一致。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class AbstractModel_ModifyDamageMultiplicative_PowerMultiplierPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(AbstractModel_ModifyDamageMultiplicative_PowerMultiplierPatch));

        private static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(AbstractModel), "ModifyDamageMultiplicative");

        private static void Postfix(object __instance, Creature? target, ValueProp props, CardModel? cardSource, ref decimal __result)
        {
            switch (__instance)
            {
                case CalamityMarkPower mark:
                    __result *= mark.DamageMultiplier(target);
                    break;
                case TingQiaoPower qiao:
                    __result *= qiao.DamageMultiplier(target);
                    break;
                case TrackedPower tracked:
                    __result *= tracked.DamageMultiplier(target, props, cardSource);
                    break;
            }
        }
    }

    /// <summary>
    /// 芝士雪豹的骨架动画（全部动作压到 idle_loop）——2026-09-25 单 DLL 双分支适配。
    /// <para>
    /// 为什么不再用 override：`CharacterModel.GenerateAnimator` 在两个分支上**不是同一套重载**——
    /// public 只有 1 参 `(MegaSprite controller)`；public-beta 把它换成了
    /// `(MegaSprite controller, Creature creature)`（1 参版在 beta 从 CharacterModel 上消失了，
    /// 本体取玩家动画改调 2 参版），而 override 必须逐字匹配签名。
    /// </para>
    /// <para>
    /// **两个重载都挂**（`TargetMethods` 逐个探测，哪个存在挂哪个）：这样无论本体调哪一个、
    /// 无论跑在哪个分支，我们的角色都拿到同一份 animator。前缀 `return false` 跳过本体实现，
    /// 与原 override 的"完全替换"语义一致。只对 `Lingdawnpao` 生效，其它角色一律放行。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class CharacterModel_GenerateAnimator_LingdawnpaoPatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(CharacterModel_GenerateAnimator_LingdawnpaoPatch));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> targets = [];
            foreach (MethodInfo? method in new[]
            {
                AccessTools.Method(typeof(CharacterModel), "GenerateAnimator", [typeof(MegaSprite)]),
                AccessTools.Method(typeof(CharacterModel), "GenerateAnimator", [typeof(MegaSprite), typeof(Creature)]),
            })
            {
                if (method != null)
                {
                    targets.Add(method);
                }
            }

            return targets;
        }

        private static bool Prefix(CharacterModel __instance, MegaSprite controller, ref CreatureAnimator __result)
        {
            if (__instance is not Lingdawnpao)
            {
                return true;
            }

            __result = Lingdawnpao.CreateIdleAnimator(controller);
            return false;
        }
    }

    /// <summary>
    /// 「厚绒」打出后不进弃牌堆、而是随机洗回抽牌堆——2026-09-25 单 DLL 双分支适配。
    /// <para>
    /// 为什么不再用 override：本体那个"出牌去向"钩子在 public 是
    /// `ModifyCardPlayResultPileTypeAndPosition(CardModel, bool, ResourceInfo, PileType, CardPilePosition)`
    /// → 返回 `(PileType, CardPilePosition)`；在 public-beta 改名
    /// `ModifyCardPlayResultLocation(CardModel, bool, ResourceInfo, CardLocation)` → 返回 `CardLocation`
    /// （record struct）——**连名字和返回类型都不同**，一份 DLL 没法同时匹配 → 改成挂**下游落堆**这一步。
    /// </para>
    /// <para>
    /// 位置选在这里的理由：`CardPileCmd.Add(CardModel, PileType, CardPilePosition, AbstractModel?, bool)`
    /// 在两个分支**签名完全一致**，而且是本体 `OnPlayWrapper` 里把"算好的去向"真正落地的唯一一步
    /// （public CardModel.cs:1621 / beta CardModel.cs:1631，两边都先判 `pile.Type == PileType.Play` 才落堆），
    /// 所以在这里改写去向与原来挂在钩子上的效果等价。
    /// </para>
    /// <para>
    /// 两道闸（照原 override 的口径）：
    /// ① `newPileType == PileType.Discard` —— 只改写"原本要进弃牌堆"的那一次；[gold]消耗[/gold]（走
    ///    `CardCmd.Exhaust`）、复制品（走 `RemoveFromCombat`）根本不会走到这个 `Add`，天然放行。
    /// ② `card.Pile?.Type == PileType.Play` —— 把范围锁死在"刚打完、正在从 Play 堆出来"的这一次；
    ///    **回合末手牌弃掉**（牌在 Hand 堆）之类的其它进弃牌堆路径一律不拦。
    /// </para>
    /// <para>
    /// ⚠️ 已知的极窄差异：本体 `CardModel.MoveToResultPileWithoutPlaying`（自动打出的牌在"不可打出 / 没有合法目标"时的
    /// 兜底落堆）也满足这两道闸 → 那种情况下本卡也会被洗回抽牌堆，而原钩子版本不会。
    /// 本卡是 Self 目标的技能牌，那条分支实际走不到；留着这段注释是怕以后忘了这个差异。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class CardPileCmd_Add_ThickCoatRecyclePatch
    {
        [HarmonyFinalizer]
        private static System.Exception? Fin(System.Exception? __exception) =>
            PatchSwallower.Swallow(__exception, nameof(CardPileCmd_Add_ThickCoatRecyclePatch));

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardPileCmd), "Add", new[]
        {
            typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool),
        });

        private static void Prefix(CardModel card, ref PileType newPileType, ref CardPilePosition position)
        {
            if (card is ThickCoat && newPileType == PileType.Discard && card.Pile?.Type == PileType.Play)
            {
                newPileType = PileType.Draw;
                position = CardPilePosition.Random;
            }
        }
    }

    /// <summary>
    /// 「暗杀者」的获取途径：本体先古遗物「欧洛巴斯之触」（奥罗巴斯 Orobas 的选项池）拾取时会把
    /// 初始遗物替换为先古版本。本体映射表是硬编码的 5 组（赤血→黑血、蛇之戒→龙之戒、神授之权→神授天命、
    /// 缚魂瓶→解缚魂瓶、裂核→注能核），查不到就退化成「圆环」Circlet。这里补上「伏击者 → 暗杀者」，
    /// 于是芝士雪豹在奥罗巴斯处选到「欧洛巴斯之触」就等于拿到暗杀者——且选项的悬停提示会自动显示
    /// 暗杀者的描述（本体那条描述用的是 {StarterRelic}/{UpgradedRelic} 动态值）。
    /// 注意 SetupForPlayer 还要求玩家身上有 Starter 稀有度遗物，伏击者正是（本体也靠它决定这个选项出不出）。
    /// </summary>
    [HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
    internal static class TouchOfOrobas_GetUpgradedStarterRelic_AssassinPatch
    {
        private static void Postfix(RelicModel starterRelic, ref RelicModel __result)
        {
            if (starterRelic is FrostFang)
            {
                __result = ModelDb.Relic<Assassin>().ToMutable();
            }
        }
    }

    /// <summary>
    /// 「决堤」的获取途径：本体先古遗物「古老牙齿」ArchaicTooth（奥罗巴斯事件选项）会把牌组里的
    /// 初始牌转换成先古版本。本体映射表硬编码 5 组（痛击→破击、中和→镇压等），这里补上「倾泄 → 决堤」：
    /// SetupForPlayer 因牌组含倾泄而可用（奥罗巴斯处选项自动出现），拾取后倾泄被替换为决堤，
    /// 升级与附魔保留（GetTranscendenceTransformedCard 原样逻辑）。
    /// 尘封魔典 DustyTome 的 where 子句排除转换表先古卡（TranscendenceCards），所以雪豹的尘封魔典仍必给狩猎形态。
    /// 注：TranscendenceUpgrades 是 private static 属性、每次访问新建字典，Postfix 补一条所有调用方都吃得到。
    /// （2026-09-21：原「偷袭 → 霜牙」映射已随霜牙 v4 回调取消，此机制改用于决堤。）
    /// </summary>
    [HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
    internal static class ArchaicTooth_TranscendenceUpgrades_DamBreakPatch
    {
        private static void Postfix(ref Dictionary<ModelId, CardModel> __result)
        {
            __result[ModelDb.Card<HuntOperation>().Id] = ModelDb.Card<DamBreak>();
        }
    }
}
