using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// ⚠️ **临时诊断补丁（雪球+ 显示 bug 排查用，定位后删除）**：
    /// 监听雪球保护每次描述渲染的入参与结果，验证升级态在显示链路里的真实值。
    /// 复现：打开牌组查看 / 任何显示雪球保护的界面，然后看 godot.log 里 [诊断雪球保护] 条目。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "GetDescriptionForPile")]
    internal static class Debug_SnowballGuardDescProbe
    {
        private static void Postfix(CardModel __instance, object previewType, string __result)
        {
            if (__instance is not SnowballGuard)
            {
                return;
            }

            Log.Info($"[诊断雪球保护] previewType={previewType} IsUpgraded={__instance.IsUpgraded} "
                + $"level={__instance.CurrentUpgradeLevel}\n[诊断雪球保护] 描述渲染结果：{__result}");
        }
    }
}
