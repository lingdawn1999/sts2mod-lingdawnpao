using System;
using MegaCrit.Sts2.Core.Logging;

namespace lingdawnpao.Core
{
    /// <summary>
    /// **方法级 Finalizer 容错**（2026-09-23 借鉴 RitsuLib/酒狐的做法）。
    /// <para>
    /// 雪豹原本只有类级容错（<see cref="ModInitializer.PatchAllTolerant" />）：补丁类注册失败会被吞掉，
    /// 但补丁**运行时**抛异常仍会冒泡到 Harmony 框架层、让本体被补丁的方法直接抛异常——
    /// <see cref="NCardGrid_AssignCardsToRow_AmbushReadyGlowPatch" /> 这种 UI 路径上的 Postfix 一旦 NRE，
    /// 本体 UI 渲染就被打断，最严重的后果是"模型消失 + 黑屏"（怀疑即青蛙骑士报障的根因）。
    /// </para>
    /// <para>
    /// 挂 Finalizer 后，单次调用的异常被吞掉、记日志，本体方法继续返回它原本的返回值
    /// （Postfix 的修改会丢失但不会污染原方法）。每个补丁类内挂一个 Finalizer 方法调本 helper 即可。
    /// </para>
    /// <para>
    /// ⚠️ Finalizer 只吞"补丁自己抛的异常"，不吞"本体方法本身"的异常——本体抛了照样抛。
    /// 它保护的是"我们的代码炸了不要连累本体"，不是"让本体别炸"。
    /// </para>
    /// </summary>
    internal static class PatchSwallower
    {
        /// <summary>
        /// 吞掉补丁运行时抛出的异常、记日志、返回 null（让 Harmony 不再重抛）。
        /// <para>
        /// ⚠️ **参数名必须叫 `__exception`** —— Harmony 是按**保留参数名**而不是按类型识别入参的。
        /// 调用方写成：`[HarmonyFinalizer] private static Exception? Fin(Exception? __exception) => PatchSwallower.Swallow(__exception, nameof(类名));`
        /// </summary>
        /// <remarks>
        /// 2026-09-23 踩过（严重）：参数名原来叫 `ex`，Harmony 认不出它，
        /// 于是**整个补丁类在注册阶段**就抛 `HarmonyException: Patching exception in method X` 被跳过。
        /// 后果是 6 个挂了 Finalizer 的补丁类全部静默失效——埋牌主拦截点 `NCardPlay.TryPlayCard` 从未生效
        /// （也就是玩家报的「伏击区不能埋牌」）。改名前不要动这个参数名。
        /// </remarks>
        public static Exception? Swallow(Exception? ex, string patchName)
        {
            if (ex != null)
            {
                Log.Warn($"芝士雪豹：补丁 {patchName} 运行时抛 {ex.GetType().Name}: {ex.Message}，已吞掉不让其冒泡到本体");
            }
            return null;
        }
    }
}
