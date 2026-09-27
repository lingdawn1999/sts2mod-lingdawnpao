using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 「伏击者」遗物图标的**激活态换图**补丁：伏击形态开启时（RelicStatus.Active），
    /// 遗物大图（牌组/遗物栏的大图预览）换成点亮的 `frost_fang_active.png`。
    /// <para>
    /// 三个挂点缺一不可（图标有三条渲染路径）：
    /// ① `RelicModel.UpdateTexture`——遗物描述面板的小图；
    /// ② `NRelicInventoryHolder.RefreshStatus`——战斗/地图 HUD 遗物栏图标
    ///    （`_model`/`_relic` 是 private，用 Traverse 取——本体改字段名时这里只会静默跳过，不会炸）；
    /// ③ `RelicModel.BigIcon` getter——遗物大图（点开遗物详情）。
    /// 图标本身常驻 `__instance.Icon`，激活态只在 `res://images/relics/frost_fang_active.png` 存在时替换。
    /// </para>
    /// </summary>
    [HarmonyPatch]
    internal static class FrostFangIconPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RelicModel), "UpdateTexture")]
        private static void UpdateTexture_Postfix(RelicModel __instance, TextureRect texture)
        {
            if (__instance is FrostFang)
            {
                texture.Texture = __instance.Icon;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NRelicInventoryHolder), "RefreshStatus")]
        private static void RefreshStatus_Postfix(NRelicInventoryHolder __instance)
        {
            RelicModel model = Traverse.Create(__instance).Field("_model").GetValue<RelicModel>();
            if (model is not FrostFang)
            {
                return;
            }

            if (Traverse.Create(__instance).Field("_relic").GetValue() is NRelic relic)
            {
                relic.Icon.Texture = model.Icon;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RelicModel), "BigIcon", MethodType.Getter)]
        private static void BigIcon_Postfix(RelicModel __instance, ref Texture2D __result)
        {
            if (__instance is FrostFang && __instance.Status == RelicStatus.Active)
            {
                string activePath = "res://images/relics/frost_fang_active.png";
                if (ResourceLoader.Exists(activePath, ""))
                {
                    __result = PreloadManager.Cache.GetTexture2D(activePath);
                }
            }
        }
    }
}
