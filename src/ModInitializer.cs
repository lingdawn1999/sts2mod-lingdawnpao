using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao
{
    [ModInitializer(nameof(Initialize))]
    public static class ModInitializer
    {
        /// <summary>本模组适配并实机验证过的本体版本：正式版（public 分支）。</summary>
        private const string AdaptedPublicVersion = "0.107.1";

        /// <summary>本模组适配并实机验证过的本体版本：测试分支（public-beta）。</summary>
        private const string AdaptedBetaVersion = "0.111.0";

        /// <summary>
        /// 兼容性自检清单：逐条检查我们**真的依赖**的本体成员还在不在。
        /// 为什么不能只靠版本号判断：本体是 EA，补丁只动几个签名也不会改版本语义，
        /// 而签名一变，我们 DLL 里的调用点会在 **JIT 时报 MissingMethodException**，
        /// 表现是"进了游戏才炸、还炸得莫名其妙"，玩家只能来评论区问。
        /// 2026-09-22 实例：public-beta 删掉了 `NExhaustVfx` 类型 → 玩家埋牌时动作队列被异常打断，
        /// 手牌与伏击区 UI 停在坏状态，就是那句"我牌呢"。
        /// <para>
        /// **一份 DLL 同时支持 public 与 public-beta 两个分支**（2026-09-25 起）：两个分支签名不同的
        /// 本体成员一律走**运行期探测**（见 src/Core/BranchCompat.cs 与 Patches.cs 里按名字解析目标的几个补丁），
        /// 所以自检分两类：
        /// ① **编译期绑定**的调用点：按**精确签名**查——IL 里记的就是那个重载，签名一变就直接
        ///    MissingMethodException；
        /// ② **运行期桥接**：直接问桥接层"你解析到了吗"（`Resolve()`）——这类问题编译器抓不到，
        ///    只有启动自检能在开局前拦下。
        /// </para>
        /// Critical = true 的项缺失时**直接拒绝加载**（宁可不让玩，也不让人进去打个坏局）；
        /// false 的只是视觉件，缺了记警告继续。
        /// </summary>
        private static readonly (string Label, bool Critical, Func<bool> Probe)[] CompatProbes =
        [
            // ── ① 编译期绑定的调用点（按精确签名查）──
            // ⚠️ 这里**只查我们真的在 IL 里直接调用的重载**，不要照抄"本体的常见重载"：
            // 2026-09-25 事故——探针里留了两条 public 时代写的 `CreatureCmd.Damage` 6 参（单体/群体），
            // 而 public-beta 把这两个重载都改成了 7 参（末尾多 `CardPlay?`），`AccessTools.Method`
            // 按参数个数精确匹配 → 两边都找不到 → 关键项缺失 → **在 beta 上直接拒绝加载**
            // （玩家看到"雪豹加载失败"）。而实际上我们的 `Damage` 调用点早已收口：
            // 只直接调**单体 5 参**（DotingMom / TingQiao），其余（要同时传 dealer + cardSource 的）
            // 全走 <see cref="CreatureCmdCompat" /> 的运行期桥接。所以探针也只需查那 5 参版。
            ("CreatureCmd.Damage（单体 5 参）", true, () => AccessTools.Method(typeof(CreatureCmd), "Damage",
            [
                typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature),
            ]) != null),
            ("CardCmd.Exhaust（4 参）", true, () => AccessTools.Method(typeof(CardCmd), "Exhaust",
            [
                typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool),
            ]) != null),
            // 只按名字找：本体的卡牌攻击构造器是 `DamageCmd.Attack(...).FromCard(this)`，
            // FromCard 可能声明在 AttackCommand 的基类上（泛型基类的参数类型匹配不可靠），
            // 所以这里顺着继承链只校验"这个名字还在"。
            ("AttackCommand.FromCard", true, () => HasMemberNamed(typeof(AttackCommand), "FromCard")),
            // ── ② 运行期桥接 / 运行期解析的补丁目标（两个分支形态不同的那几处，直接问它"解析到了吗"）──
            ("AttackCommand.FromCard（跨分支桥接）", true, AttackCommandCompat.Resolve),
            ("CreatureCmd.Damage（跨分支桥接）", true, CreatureCmdCompat.Resolve),
            // 狩猎形态"攻击无视格挡"的补丁挂点：群体伤害重载 public 6 参 / beta 7 参，
            // 探不到就必须拦下——那种情况下补丁会静默失效（不崩，但机制丢了）。
            ("CreatureCmd.Damage（群体 6/7 参 · 狩猎形态补丁挂点）", true,
                () => CreatureCmd_Damage_CalamityFormPatch.TargetOrNull() != null),
            // ── ③ 视觉件（缺了只影响表现）──
            ("埋牌消散特效（NExhaustVfx / NCardExhaustVfx）", false, ExhaustVfxCompat.Resolve),
        ];

        public static void Initialize()
        {
            VerifyGameApi();

            var harmony = new Harmony("lingdawnpao.zhishixuebao");
            PatchAllTolerant(harmony);

            // 骨架资源预热（2026-09-23「进 boss 关全场无模型 + 存档重进黑屏」事故的防线之一）：
            // 在初始化这个干净时机把资产读进常驻缓存，战斗/房间切换路径只读缓存、不再读盘。
            LingdawnpaoVisuals.Warmup();

            ModHelper.AddModelToPool<LingdawnpaoCardPool, SneakAttack>();
            ModHelper.AddModelToPool<LingdawnpaoCardPool, HuntOperation>();
            ModHelper.AddModelToPool<LingdawnpaoCardPool, TigerCombo>();

            // 观星（2026-09-22 用户要求）：改挂**无色池** —— 商店会卖（MerchantInventory 就是取
            // ColorlessCardPool.GetUnlockedCards）、卡牌百科归到"无色"，且所有角色都能拿到。
            // 注意它与上面三张不同：那三张是"雪豹池自有卡"，这里是把一张卡**送给本体池**。
            // 必须在游戏初始化前调用（ModHelper 会在池首次展开内容时冻结，之后再加会抛异常）。
            ModHelper.AddModelToPool<ColorlessCardPool, Stargazing>();

            // 电子烟（2026-09-22）：无色蓝卡，全角色商店可刷。
            ModHelper.AddModelToPool<ColorlessCardPool, Vape>();

            // ── Token 衍生牌必须挂进 TokenCardPool（2026-09-22 修「一个关卡后卡死」）──
            // 起因：本体的 `CardModel.Pool`（CardModel.cs:297）先遍历 AllCardPools，
            // **找不到父池时会回退去查 `ModelDb.CardPool<MockCardPool>().AllCardIds`**，
            // 而 MockCardPool.GenerateAllCards 里 MockCanonical() 直接
            // `NeverEverCallThisOutsideOfTests_ClearOwner()` → **抛 InvalidOperationException "You monster!"**。
            // 我们的衍生牌此前没进任何池 → 一旦游戏为它取 Pool（如 NCard.UpdateEnergyCostColor），
            // 就在**回合开始的异步钩子**里炸掉：`SnowfieldPower.AfterSideTurnStart → Snowball.CreateIntoHandAsync
            // → CardPileCmd.AddGeneratedCardsToCombat → UpdateEnergyCostColor → get_Pool → 抛异常`，
            // 导致 CombatManager 的回合推进任务中断 → 埋牌动作不执行、之后出不了牌。
            // 修法：把全部 Token 稀有度衍生牌登记进本体的 TokenCardPool（= 它们语义上该在的地方）。
            // 注意：**只挂 TokenCardPool，不要同时挂 LingdawnpaoCardPool**——Pool 取第一个命中的池。
            // 【v2 退役归档】「雪球」「大雪球」「呼吸」已随雪球线/永恒呼吸下架（整线退役方案 A），
            // 不再登记进 TokenCardPool；卡牌类文件保留作归档。
            ModHelper.AddModelToPool<TokenCardPool, AmbushStrike>();
            ModHelper.AddModelToPool<TokenCardPool, Bharal>();
            ModHelper.AddModelToPool<TokenCardPool, DotingMom>();
            ModHelper.AddModelToPool<TokenCardPool, DullYak>();
            ModHelper.AddModelToPool<TokenCardPool, FierceFoxCombo>();
            ModHelper.AddModelToPool<TokenCardPool, FoxTutor>();
            ModHelper.AddModelToPool<TokenCardPool, GreedyMarmot>();
            ModHelper.AddModelToPool<TokenCardPool, NatureBlessing>();
            ModHelper.AddModelToPool<TokenCardPool, NoisyRaven>();
            ModHelper.AddModelToPool<TokenCardPool, PallasCat>();
            ModHelper.AddModelToPool<TokenCardPool, Prey>();
            ModHelper.AddModelToPool<TokenCardPool, Snowball>();
            ModHelper.AddModelToPool<TokenCardPool, SnowShelter>();
            ModHelper.AddModelToPool<TokenCardPool, TibetanFox>();
            // 【v2 吞噬合成轴】合体卡（FusionCard）：按实例动态的合成产物，只挂 TokenCardPool
            // （ModelDb 需要类型注册；不进 LingdawnpaoCardPool——产物不该出现在奖励/图鉴里）
            ModHelper.AddModelToPool<TokenCardPool, FusionCard>();

            Log.Info("芝士雪豹 mod 加载成功");
        }

        /// <summary>
        /// 兼容性闸门：关键 API 缺失时**抛出异常拒绝加载**，而不是带病进游戏。
        /// 本体对初始化异常的处理（ModManager.CallModInitializer → false）会把这个模组
        /// 在"模组列表"里标成加载失败（MOD_ERROR.ASSEMBLY_LOAD），玩家看得到；
        /// 此时我们还没打任何补丁、没注册任何卡池，所以角色不会出现在选人界面，也就进不了坏局。
        /// </summary>
        private static void VerifyGameApi()
        {
            List<string> missing = [];
            foreach ((string label, bool critical, Func<bool> probe) in CompatProbes)
            {
                bool ok;
                try
                {
                    ok = probe();
                }
                catch (Exception e)
                {
                    Log.Warn($"芝士雪豹：兼容性自检「{label}」执行异常（{e.GetType().Name}: {e.Message}）");
                    ok = false;
                }

                if (ok)
                {
                    continue;
                }

                Log.Error($"芝士雪豹：兼容性自检未通过，找不到「{label}」{(critical ? "（关键）" : "（仅视觉）")}");
                if (critical)
                {
                    missing.Add(label);
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            throw new NotSupportedException(
                $"芝士雪豹 mod 未适配当前本体版本，缺少本体 API：{string.Join("、", missing)}。"
                + $"本模组一份 DLL 同时支持正式版（public，v{AdaptedPublicVersion}）"
                + $"与测试分支（public-beta，v{AdaptedBetaVersion}）；"
                + "若本体刚更新到更新的版本，请等本模组跟进后再启用（可先切回上一条已知可用的分支并重启游戏）。");
        }

        /// <summary>顺着继承链按名字找成员（不管签名），供无法精确匹配参数类型的探测使用。</summary>
        private static bool HasMemberNamed(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (Type? t = type; t != null; t = t.BaseType)
            {
                foreach (MethodInfo method in t.GetMethods(flags))
                {
                    if (method.Name == name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 逐类应用补丁。本体的 `Harmony.PatchAll(assembly)` 是整体调用：
        /// 任何一个补丁类的 `TargetMethod()` 返回 null（目标方法在新版本里改名/改了签名），
        /// Harmony 就抛异常**中断循环**，排在它后面的补丁类**一个都不会应用**。
        /// 2026-09-22 在 public-beta 上实测到了这个后果：`CalamityForm` 补丁失败，
        /// 导致角色注册、卡池过滤、右键切形态等补丁全部没生效，玩家看到的是"模组半死不活"
        /// 而不是"版本不兼容"。这里换成每个补丁类单独 apply + 单独 catch，失败只记警告。
        /// </summary>
        private static void PatchAllTolerant(Harmony harmony)
        {
            List<string> failed = [];
            int applied = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(ModInitializer).Assembly))
            {
                if (!IsPatchClass(type))
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failed.Add(type.Name);
                    // ⚠️ 内层异常必须打出来：Harmony 的 HarmonyException 正文只有一句
                    // "Patching exception in method X"，真正的原因（哪个参数不被识别、签名哪里不匹配）
                    // 全在 InnerException 里。2026-09-23 就是缺了这条信息，6 个补丁类静默失效却查不出原因。
                    Log.Warn($"芝士雪豹：补丁类 {type.Name} 应用失败（{e.GetType().Name}: {e.Message}），已跳过该类，其余补丁继续"
                        + DescribeInnerExceptions(e));
                }
            }

            Log.Info(failed.Count == 0
                ? $"芝士雪豹：Harmony 补丁类全部应用成功（{applied} 个）"
                : $"芝士雪豹：Harmony 补丁已应用 {applied} 个，{failed.Count} 个失败：{string.Join("、", failed)}");
        }

        /// <summary>把 InnerException 链拼成一行日志（Harmony 补丁失败的真因都藏在里层）。</summary>
        private static string DescribeInnerExceptions(Exception e)
        {
            string text = string.Empty;
            for (Exception? inner = e.InnerException; inner != null; inner = inner.InnerException)
            {
                text += $"｜内层 {inner.GetType().Name}: {inner.Message}";
            }

            return text;
        }

        /// <summary>判断一个类型是不是补丁类（类上有 [HarmonyPatch]，或类里有带补丁注解的方法）。</summary>
        private static bool IsPatchClass(Type type)
        {
            if (!type.IsClass)
            {
                return false;
            }

            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
            {
                return true;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0
                    || method.GetCustomAttributes(typeof(HarmonyPrefix), true).Length > 0
                    || method.GetCustomAttributes(typeof(HarmonyPostfix), true).Length > 0
                    || method.GetCustomAttributes(typeof(HarmonyTranspiler), true).Length > 0
                    || method.GetCustomAttributes(typeof(HarmonyFinalizer), true).Length > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}