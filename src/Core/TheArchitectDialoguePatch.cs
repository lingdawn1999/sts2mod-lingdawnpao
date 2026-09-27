using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace lingdawnpao.Core
{
    /// <summary>
    /// **建筑师（终局事件）的雪豹专属台词**（2026-09-23 用户定稿：雪豹先扑 → 建筑师「是谁在打扰我」
    /// → 雪豹「嗷嗷」→ 建筑师「雪豹闭嘴」→ 建筑师还手；3 行台词）。
    /// <para>
    /// 这套对话由本体<b>先古对话系统</b>驱动（`AncientDialogueSet`）：每个角色一组
    /// `AncientDialogue`，**文本不在代码里**，而是按 `ancients` 表里的键去取；键的规则是
    /// `THE_ARCHITECT.talk.<角色Id>.对话序号-行序号.说话方`，说话方取 `.ancient`（建筑师）或
    /// `.char`（玩家角色）——**哪一侧的键存在就由谁来说这句话**（见 `AncientDialogue.PopulateLines`）。
    /// 我们的两句因此写成本 mod 的 `localization/{zhs,eng}/ancients.json`。
    /// </para>
    /// <para>
    /// **台词顺序 ≠ 任意编排**：本体只在两个时刻放攻击演出——`PlayCurrentLine` 里**第 0 行之前**
    /// （`StartAttackers`）与对话结束后的 `WinRun`（`EndAttackers`）。所以能做到的最接近编排是
    /// **「雪豹先动手 → 雪豹『嗷嗷』→ 建筑师『雪豹闭嘴』→ 建筑师还手」**；
    /// 想让"雪豹说完再动手"需要本体支持行间攻击，目前做不到。
    /// 另外这两个攻击都是**演出**（`AnimPlayerAttackIfNecessary` 播角色自己的
    /// `GetArchitectAttackVfx`、`AnimArchitectAttackIfNecessary` 播闪电+火焰特效），**不结算任何伤害**。
    /// </para>
    /// <para>
    /// 键上的 `r` 后缀 = 放进"重复对话池"（本体 `IRONCLAD` 那组就是全 `r`），
    /// 于是**每次**见建筑师都会演这段；同一段对话的**每一行都必须带 `r`**，
    /// 否则 `PopulateLines` 会抛 `InvalidOperationException`（本体硬校验，见其注释）。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(TheArchitect), "DefineDialogues")]
    internal static class TheArchitect_DefineDialogues_LingdawnpaoPatch
    {
        private static void Postfix(ref AncientDialogueSet __result)
        {
            string charEntry = ModelDb.Character<Lingdawnpao>().Id.Entry;
            __result.CharacterDialogues[charEntry] =
            [
                new AncientDialogue("", "")
                {
                    // 第 0 行之前：雪豹扑上去；对话结束：建筑师还手
                    // 台词流程：是谁在打扰我(ancient) → 嗷嗷(char) → 雪豹闭嘴(ancient)
                    StartAttackers = ArchitectAttackers.Player,
                    EndAttackers = ArchitectAttackers.Architect,
                },
            ];
            Log.Info($"建筑师对话：已注入 {charEntry} 的专属台词（3 行，重复池）");
        }
    }
}