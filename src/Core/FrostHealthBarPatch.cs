using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 冰冻血条动画：仿本体灾厄的 DoomForeground，在血条上叠加一条冰蓝色的"死亡区"
    /// （宽度 = 冰冻层数 / 最大生命值，覆盖在血条左侧）。
    /// 实现：NHealthBar.RefreshValues 后置补丁，懒创建一个 PoisonForeground 的克隆节点
    /// （同贴图同九宫格，只换冰蓝自染色，不用灾厄的紫色 shader 材质），插到
    /// DoomForeground 之后绘制，每次刷新按当前冰冻层数重算宽度。
    /// 生命值低于层数会瞬间处决（FrostbitePower.AfterDamageReceived），不存在持久的
    /// "必死"状态，因此不做灾厄那种致命变色的分支。
    /// </summary>
    [HarmonyPatch(typeof(NHealthBar), "RefreshValues")]
    internal static class NHealthBar_RefreshValues_FrostZonePatch
    {
        private const string FrostNodeName = "LingdawnpaoFrostForeground";

        private static readonly Color FrostColor = new Color(0.40f, 0.75f, 1.00f);

        private static readonly FieldInfo DoomFgField = AccessTools.Field(typeof(NHealthBar), "_doomForeground");
        private static readonly FieldInfo PoisonFgField = AccessTools.Field(typeof(NHealthBar), "_poisonForeground");
        private static readonly FieldInfo HpFgContainerField = AccessTools.Field(typeof(NHealthBar), "_hpForegroundContainer");
        private static readonly FieldInfo CreatureField = AccessTools.Field(typeof(NHealthBar), "_creature");
        private static readonly FieldInfo ExpectedMaxFgWidthField = AccessTools.Field(typeof(NHealthBar), "_expectedMaxFgWidth");

        private static void Postfix(NHealthBar __instance)
        {
            try
            {
                var creature = (Creature?)CreatureField.GetValue(__instance);
                var poisonFg = (NinePatchRect?)PoisonFgField.GetValue(__instance);
                var doomFg = (Control?)DoomFgField.GetValue(__instance);
                var container = (Control?)HpFgContainerField.GetValue(__instance);
                if (creature == null || poisonFg == null || doomFg == null || container == null
                    || !GodotObject.IsInstanceValid(poisonFg))
                {
                    return;
                }

                NinePatchRect frostFg = poisonFg.GetParent().GetNodeOrNull<NinePatchRect>(FrostNodeName);
                if (frostFg == null)
                {
                    frostFg = (NinePatchRect)poisonFg.Duplicate();
                    frostFg.Name = FrostNodeName;
                    frostFg.SelfModulate = FrostColor;
                    frostFg.Visible = false;
                    poisonFg.GetParent().AddChild(frostFg);
                    // 插到 DoomForeground 之后：两者同挂时冰冻画在灾厄上面
                    poisonFg.GetParent().MoveChild(frostFg, doomFg.GetIndex() + 1);
                }

                decimal? frostAmount = creature.GetPower<FrostbitePower>()?.Amount;
                int frost = (int)(frostAmount ?? 0m);

                if (creature.CurrentHp <= 0 || creature.HpDisplay.IsInfinite() || frost <= 0)
                {
                    frostFg.Visible = false;
                    return;
                }

                float expected = ExpectedMaxFgWidthField.GetValue(__instance) is float f ? f : -1f;
                float maxFgWidth = expected > 0f ? expected : container.Size.X;

                frostFg.Visible = true;
                frostFg.OffsetLeft = 0f;
                // 与本体灾厄同款算法：右缘 = 层数宽度 + 九宫格右边距，超出条宽则夹到满条
                frostFg.OffsetRight = Math.Min(0f, FgWidth(creature, frost, maxFgWidth) - maxFgWidth + frostFg.PatchMarginRight);
            }
            catch (Exception)
            {
                // 血条属于纯视觉层，任何异常都不能打断战斗流程
            }
        }

        // NHealthBar.GetFgWidth 的私有逻辑复刻
        private static float FgWidth(Creature creature, int amount, float maxFgWidth)
        {
            if (creature.MaxHp <= 0)
            {
                return 0f;
            }
            float val = (float)amount / creature.MaxHp * maxFgWidth;
            return Math.Max(val, creature.CurrentHp > 0 ? 12f : 0f);
        }
    }
}
