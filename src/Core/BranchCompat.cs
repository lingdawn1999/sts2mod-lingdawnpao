using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>Exhaust returns Task on public and Task&lt;CardPileAddResult?&gt; on beta.</summary>
    internal static class CardCmdCompat
    {
        private static readonly MethodInfo? ExhaustMethod = typeof(CardCmd).GetMethod("Exhaust",
            BindingFlags.Public | BindingFlags.Static, null,
            [typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool)], null);

        public static bool Resolve() => ExhaustMethod != null
            && typeof(Task).IsAssignableFrom(ExhaustMethod.ReturnType);

        public static async Task Exhaust(PlayerChoiceContext context, CardModel card,
            bool causedByEthereal = false, bool skipVisuals = false)
        {
            if (!Resolve()) throw new NotSupportedException("芝士雪豹：找不到兼容的 CardCmd.Exhaust 接口");
            Task task;
            try
            {
                task = (Task)ExhaustMethod!.Invoke(null, [context, card, causedByEthereal, skipVisuals])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            await task;
        }
    }

    /// <summary>
    /// 双分支兼容层（2026-09-25）：**一份 DLL 同时跑正式版（public）与测试分支（public-beta）**。
    /// <para>
    /// **为什么不能用条件编译**：那样得编两份 DLL、发两个工坊条目（玩家装错就炸）；
    /// 而本体在 public-beta 动过的手续里，有两处是"调用点参数个数不同"，编译期绑定的重载
    /// 不可能同时匹配两个分支。所以改成**运行期探测**：启动时按名字+参数类型找到对应分支的那个重载、
    /// 缓存委托，之后照常调用。源码里因此只保留"两分支都存在"的编译期引用。
    /// </para>
    /// <para>
    /// **本文件负责的两处**（其余 override/补丁见 Patches.cs 与 <see cref="Lingdawnpao" />）：
    /// ① `AttackCommand.FromCard`：public 是 `(CardModel)`、public-beta 是 `(CardModel, CardPlay?)`；
    /// ② `CreatureCmd.Damage`：两个分支都有的 5 参重载等价于 `cardSource = null`（实测两边都这么转发），
    ///    所以只有**同时要传 dealer 与 cardSource** 的调用点需要桥接，public 落到 6 参、
    ///    public-beta 落到 7 参（末尾多个 `CardPlay?`）。
    /// </para>
    /// <para>
    /// **维护约定**：本体再改签名时，优先在本文件加一个桥接方法；桥接的"能不能解析"必须能被
    /// <see cref="ModInitializer" /> 的启动自检问到（<c>Resolve()</c>），探不到就**拒绝加载**——
    /// 因为这类问题编译器不会再替你发现，只能靠自检在启动时拦下。
    /// 另外：改完代码记得对着两个参考程序集各编一次（`-p:Sts2Ref=beta`），两边都必须 0 错误。
    /// </para>
    /// </summary>
    internal static class AttackCommandCompat
    {
        private delegate AttackCommand FromCardWithPlay(AttackCommand command, CardModel card, CardPlay? cardPlay);

        private delegate AttackCommand FromCardOnly(AttackCommand command, CardModel card);

        private static FromCardWithPlay? _withPlay;

        private static FromCardOnly? _only;

        private static bool _resolved;

        /// <summary>启动自检用：探测本体 `AttackCommand.FromCard` 属于哪个分支的签名。</summary>
        public static bool Resolve()
        {
            if (_resolved)
            {
                return _withPlay != null || _only != null;
            }

            _resolved = true;
            try
            {
                // public-beta：FromCard(CardModel, CardPlay?)
                MethodInfo? withPlay = AccessTools.Method(typeof(AttackCommand), "FromCard", [typeof(CardModel), typeof(CardPlay)]);
                if (withPlay != null)
                {
                    _withPlay = (FromCardWithPlay)withPlay.CreateDelegate(typeof(FromCardWithPlay));
                }

                // public：FromCard(CardModel)
                MethodInfo? only = AccessTools.Method(typeof(AttackCommand), "FromCard", [typeof(CardModel)]);
                if (only != null)
                {
                    _only = (FromCardOnly)only.CreateDelegate(typeof(FromCardOnly));
                }
            }
            catch (Exception e)
            {
                Log.Warn($"芝士雪豹：探测 AttackCommand.FromCard 失败（{e.GetType().Name}: {e.Message}）");
            }

            return _withPlay != null || _only != null;
        }

        /// <summary>
        /// `AttackCommand.FromCard` 的跨分支入口。调用方一律传**当前打出的** `cardPlay`；
        /// 没有卡牌上下文（遗物 / 能力自动打出）时传 null。
        /// </summary>
        public static AttackCommand FromCardCompat(this AttackCommand command, CardModel card, CardPlay? cardPlay)
        {
            _ = Resolve();
            if (_withPlay != null)
            {
                return _withPlay(command, card, cardPlay);
            }

            if (_only != null)
            {
                return _only(command, card);
            }

            throw new NotSupportedException(
                "芝士雪豹：找不到本体的 AttackCommand.FromCard（启动自检本应拦住，请把 godot.log 发给作者）");
        }
    }

    /// <summary>
    /// `CreatureCmd.Damage` 的跨分支桥接，说明见 <see cref="AttackCommandCompat" />。
    /// 只给**同时要传 `dealer` 与 `cardSource`** 的调用点用。
    /// </summary>
    internal static class CreatureCmdCompat
    {
        private static MethodInfo? _damage;

        private static bool _needsCardPlay;

        private static bool _resolved;

        /// <summary>启动自检用：探测本体 `CreatureCmd.Damage` 在这个分支上的参数个数。</summary>
        public static bool Resolve()
        {
            if (_resolved)
            {
                return _damage != null;
            }

            _resolved = true;
            try
            {
                Type[] withPlay =
                [
                    typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal),
                    typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay),
                ];
                Type[] withoutPlay = withPlay[..^1];

                _damage = AccessTools.Method(typeof(CreatureCmd), "Damage", withPlay);
                if (_damage != null)
                {
                    _needsCardPlay = true;
                    return true;
                }

                _damage = AccessTools.Method(typeof(CreatureCmd), "Damage", withoutPlay);
            }
            catch (Exception e)
            {
                Log.Warn($"芝士雪豹：探测 CreatureCmd.Damage 失败（{e.GetType().Name}: {e.Message}）");
            }

            return _damage != null;
        }

        public static async Task Damage(PlayerChoiceContext choiceContext, Creature target, decimal amount,
            ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
        {
            _ = Resolve();
            if (_damage == null)
            {
                throw new NotSupportedException(
                    "芝士雪豹：找不到本体的 CreatureCmd.Damage（启动自检本应拦住，请把 godot.log 发给作者）");
            }

            object?[] args = _needsCardPlay
                ? [choiceContext, target, amount, props, dealer, cardSource, cardPlay]
                : [choiceContext, target, amount, props, dealer, cardSource];

            if (_damage.Invoke(null, args) is Task task)
            {
                await task;
            }
        }
    }

    /// <summary>
    /// 埋牌消散特效的跨分支桥接：public 是 `NExhaustVfx`，public-beta 换成了 `NCardExhaustVfx`
    /// （旧类型被删除）。两边的工厂签名一致（`Create(NCard)` → 节点），所以**按名字找类型、反射调 Create**——
    /// IL 里不直接引用这两个类型就不会有 TypeLoadException。
    /// 纯视觉件：找不到就返回 null，埋牌功能照常（只是没有消散特效）。
    /// </summary>
    internal static class ExhaustVfxCompat
    {
        private static readonly MethodInfo? Create = Find("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NExhaustVfx")
            ?? Find("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardExhaustVfx");

        /// <summary>启动自检用（非关键项）。</summary>
        public static bool Resolve() => Create != null;

        public static Node? CreateVfx(NCard card) => Create?.Invoke(null, [card]) as Node;

        private static MethodInfo? Find(string typeName)
        {
            Type? type = AccessTools.TypeByName(typeName);
            return type == null ? null : AccessTools.Method(type, "Create");
        }
    }
}
