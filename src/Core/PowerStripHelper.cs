using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// **"碰别人身上的能力"共用的安全闸**——如今的唯一使用者是
    /// 「破绽」（获得时随机**剥掉**该敌人一条增益）。凡是要照抄或拿取别人身上能力的效果，都必须先过这里。
    /// （本闸当初是为两张卡建的：「拟态」（照抄敌方增益）与「破绽」（剥掉一条）；
    /// 「拟态」已于 2026-09-25 整张删除——用户判定"这卡有点原因不明了"——闸本身保留给「破绽」用。）
    /// <para>
    /// **2026-09-24 用户定案："所有这类效果都不要包含转阶段和 boss 核心机制"**——起因是 2026-09-22
    /// 那次事故："移除了 boss 的增益，导致自己立刻死亡"（「无厌沙虫」The Insatiable 的吞人倒计时
    /// `SandpitPower` 是 `PowerType.Buff`，拆掉即 `CreatureCmd.Kill(玩家, force: true)`）。
    /// 因此设**两道闸**，两条都过才允许碰：
    /// </para>
    /// <para>
    /// ① `InstanceType != None` 一律跳过。本体把"机制 / 状态机"类能力**全部标成
    /// `Instanced` 或 `InstancedPerApplier`**（21 处：沙虫 `SandpitPower`、`TheBombPower`、
    /// `MagicBombPower`、`NightmarePower`、`StranglePower`、`OblivionPower`、`RollingBoulderPower`、
    /// `GuardedPower`、`ToricToughnessPower`、`ThieveryPower`、`HeistPower`、`MonologuePower` 等），
    /// 它们靠"多条独立实例"记账（`FindExistingInstanceForStacking` 直接返回 null，不走叠加），
    /// 拆掉一条就是把状态机拆坏。
    /// </para>
    /// <para>
    /// ② 自己重写过 `PowerModel.AfterRemoved` 的一律跳过——移除会触发副作用。本体 10 个：
    /// `BurrowedPower`（移除即 `LoseBlock(999999999)`，Tunneler 钻地）、`SandpitPower`（移除即吞人秒杀玩家）、
    /// `DampenPower`、`HexPower`、`RingingPower`、`ShrinkPower`、`SlumberPower`、`SwordSagePower`、
    /// `TangledPower`、`VitalSparkPower`。第二道闸对"玩家自己写的机制能力"同样有效，是更通用的一层。
    /// </para>
    /// <para>
    /// **⚠️ 刻意不设"`AfterApplied` 排除闸"**（2026-09-24 用户纠正了我的过度保守判断）：本体有 18 个能力
    /// 重写了 `AfterApplied`，但重写者**本来就允许玩家获得**——「玛扎莱斯的礼物」药水给我方挂 `RitualPower`，
    /// 「磐石护甲」「永恒护甲」「中子神盾」给我方挂 `PlatingPower`，`BarricadePower`（壁垒）/`TankPower`/
    /// `JugglingPower`/`PhantomBladesPower`/`FeralPower` 本身就是玩家卡牌能力，它们的 `AfterApplied`
    /// 都按 `Owner.IsEnemy` / `Owner.Side` 分支写。所以复制 / 获得后自然触发是**正确行为**，不该排除；
    /// 排掉反而会把 boss 身上最值得抢的增益（仪式、硬化）全滤光，让这类"照抄/拿取"效果拿到空结果。
    /// </para>
    /// <para>
    /// 口径：**宁可漏碰，绝不碰出副作用**。被拦下的条目由调用方用 <see cref="SkippedNames" /> 打日志，
    /// 方便复盘"这张 boss 卡为什么碰不了"。
    /// </para>
    /// </summary>
    internal static class PowerStripHelper
    {
        /// <summary>这条能力能不能安全移除（两道闸都过才算）。</summary>
        internal static bool CanSafelyStrip(PowerModel power)
            => power.InstanceType == PowerInstanceType.None && !OverridesAfterRemoved(power);

        /// <summary>取某个生物身上指定类型、且**允许拆**的能力清单。</summary>
        internal static List<PowerModel> SafePowers(Creature creature, PowerType type)
            => creature.Powers.Where(p => p.Type == type && CanSafelyStrip(p)).ToList();

        /// <summary>同类型但被安全闸拦下的能力名（只用于日志复盘）。</summary>
        internal static List<string> SkippedNames(Creature creature, PowerType type)
            => creature.Powers
                .Where(p => p.Type == type && !CanSafelyStrip(p))
                .Select(p => p.GetType().Name)
                .ToList();

        /// <summary>
        /// 从 <paramref name="target" /> 身上**随机拆一条**指定类型的能力；没有可拆项则返回 null。
        /// 随机源用 `RunState.Rng.CombatCardSelection`（战斗内的随机选牌流，同「动物朋友」「占卜术」），
        /// **联机下各端取到同一条**。
        /// </summary>
        internal static async Task<PowerModel?> StripRandomAsync(Creature target, PowerType type, Player rngOwner)
        {
            List<PowerModel> safe = SafePowers(target, type);
            if (safe.Count == 0)
            {
                return null;
            }

            PowerModel? picked = rngOwner.RunState.Rng.CombatCardSelection.NextItem(safe);
            if (picked == null)
            {
                return null;
            }

            await PowerCmd.Remove(picked);
            return picked;
        }

        /// <summary>闸门 ②：判别这条能力是否自己重写过 <c>AfterRemoved</c>（重写 = 移除有副作用）。</summary>
        private static bool OverridesAfterRemoved(PowerModel power)
        {
            MethodInfo? afterRemoved = power.GetType().GetMethod(nameof(PowerModel.AfterRemoved));
            return afterRemoved != null && afterRemoved.DeclaringType != typeof(PowerModel);
        }
    }
}