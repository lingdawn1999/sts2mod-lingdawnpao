using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 吞噬（合成）引擎（v2，设计稿第三节）。职责只有三件：
    /// <para>
    /// ① **材料轴的通用读取**——攻击材料的每段伤害 / 段数 / 非伤害特效：
    ///    实现 <see cref="IFusionMaterial" /> 的卡以声明为准，否则回落
    ///    （每段伤害读 <c>DynamicVars.Damage</c>、段数 1、无特效）。
    ///    技能材料不走这里——技能合体是"整张免费重放"（<see cref="FusionCard.OnPlay" /> 里的
    ///    AutoPlay 管线），同轴自然相加、异轴自然拼接，技能牌一律零接线成本。
    /// ② **可合成判定**——攻击/技能两张同类型即可；能力牌不参与（设计稿定稿）；
    ///    X 费牌 v1 不参与（X 值与"费用相加、兑现付费"口径纠缠，留作扩展）；
    ///    状态/诅咒/Token 无类型限制，但类型不同天然配不成对（晕眩+晕眩同为 Status——
    ///    若将来要开放"垃圾互吃"，只需放开这里）。
    /// ③ **合成执行**——<see cref="TryFuseAsync" />：两材料移出区域（计数 -2）→
    ///    <see cref="FusionCard" /> 经 <c>CreateCard</c> 生成并 <c>Configure</c> →
    ///    产物埋入区域**顶部**（最先被兑现带出）。材料本体既不进弃牌堆也不进消耗堆：
    ///    它们的引用被产物持有（攻击材料供特效执行、技能材料供兑现时重放），
    ///    战斗结束随战斗状态一并回收。
    /// </para>
    /// <para>
    /// **联机**：全部动作跑在调用方的同步动作队列里（「吞噬」的 OnPlay / 选牌同步管线），
    /// 不引入新的同步动作类型；两端的区域计数、产物数据、材料引用按同一顺序演进。
    /// </para>
    /// </summary>
    public static class FusionEngine
    {
        /// <summary>
        /// 这张牌能不能当合成材料。区域归属与"两张同类型"由调用方保证（<see cref="TryFuseAsync" /> 再验一遍）。
        /// </summary>
        public static bool IsFusable(CardModel card)
        {
            // 能力牌不参与（设计稿：两条被动合一强度最难算，第一版不开放）
            if (card.Type is not (CardType.Attack or CardType.Skill))
            {
                return false;
            }

            // X 费牌 v1 不参与（X 值口径纠缠，见类注释）
            if (card.EnergyCost.CostsX)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 材料的每段基础伤害（活值）。<see cref="IFusionMaterial.FusionDamagePerSegment" /> 声明优先，
        /// 否则回落 <c>DynamicVars.Damage</c>（无伤害变量的材料 = 0 段伤害贡献）。
        /// </summary>
        public static decimal DamagePerSegment(CardModel material)
        {
            if (material is IFusionMaterial declared && declared.FusionDamagePerSegment >= 0m)
            {
                return declared.FusionDamagePerSegment;
            }

            return material.DynamicVars.TryGetValue("Damage", out var damage) && damage is DamageVar damageVar
                ? damageVar.BaseValue
                : 0m;
        }

        /// <summary>材料的段数（声明优先，默认 1；埋伏分支的加段不算——材料按基础形态贡献）。</summary>
        public static int SegmentsOf(CardModel material) =>
            material is IFusionMaterial declared ? declared.FusionSegments : 1;

        /// <summary>
        /// 材料的非伤害特效部分（攻击合体用）：材料实现了 <see cref="IFusionMaterial" /> 就把它自己
        /// 当特效载体（<see cref="FusionCard" /> 也实现该接口——合体卡再合成时特效链自然递归拼接）。
        /// </summary>
        public static IEnumerable<IFusionMaterial> FusionParts(CardModel material) =>
            material is IFusionMaterial part ? new[] { part } : System.Array.Empty<IFusionMaterial>();

        /// <summary>
        /// 执行合成：校验 → 两材料移出区域（计数 -2，埋伏老兵记账一并清）→ 生成产物并配置 →
        /// 产物埋入区域顶部。返回产物（校验失败返回 null，调用方照「设伏」旧约：不满足即空放）。
        /// </summary>
        /// <param name="baseMaterial">基底材料（决定产物卡名卡图），必须已在区域内。</param>
        /// <param name="fuelMaterial">燃料材料（只贡献效果段），必须已在区域内且与基底同类型。</param>
        public static async Task<FusionCard?> TryFuseAsync(
            PlayerChoiceContext choiceContext, Player player, ICombatState combat,
            CardModel baseMaterial, CardModel fuelMaterial, string source = "吞噬")
        {
            AmbushZoneState zone = AmbushZone.For(player);
            if (baseMaterial.Type != fuelMaterial.Type
                || !IsFusable(baseMaterial) || !IsFusable(fuelMaterial)
                || !zone.Pile.Cards.Contains(baseMaterial) || !zone.Pile.Cards.Contains(fuelMaterial))
            {
                Log.Info($"吞噬：合成校验未通过（{baseMaterial.Id.Entry} + {fuelMaterial.Id.Entry}），放弃");
                return null;
            }

            // 两材料移出区域（引用被产物持有；埋伏老兵记账随之作废）。
            // 不发埋入类事件——囤粮/伏击本能等"加入区域"钩子不该被"移出"误触发。
            zone.Pile.RemoveInternal(baseMaterial);
            zone.BuriedTurn.Remove(baseMaterial);
            zone.Pile.RemoveInternal(fuelMaterial);
            zone.BuriedTurn.Remove(fuelMaterial);
            // 计数变化自己报（-2）；产物入区时 BuryGeneratedAsync 会报 +1
            await AmbushZone.NotifyCountChangedAsync(choiceContext, player, -2);
            // 材料里带着的 +力/+敏随重算移除（BuryGeneratedAsync 里还会对产物再重算一次，这里先稳住中间态）
            await AmbushZone.RecomputeBonusesAsync(choiceContext, player);

            FusionCard fusion = combat.CreateCard<FusionCard>(player);
            fusion.Configure(baseMaterial, fuelMaterial);
            Log.Info($"吞噬：{baseMaterial.Id.Entry} + {fuelMaterial.Id.Entry} → 合体卡"
                + $"（{fusion.Type}，{fusion.EnergyCost.Canonical} 费，当前 {zone.Count}/{zone.Capacity}）");

            // 产物入区顶部（设计稿：最先被兑现带出）。区域满了会塞不进——
            // 材料已移出腾出 2 格，正常流程塞得进；万一被并发钩子占满，产物丢弃（宁可放过不可崩存档）。
            if (!await AmbushZone.BuryGeneratedAsync(choiceContext, player, fusion, source, pileIndex: 0))
            {
                Log.Info("吞噬：产物未能入区（区域被占满），产物丢弃");
            }

            AmbushZoneUi.Refresh();
            return fusion;
        }

        /// <summary>
        /// 技能材料重放后的收尾：AutoPlay 会把材料按普通结算路由（非消耗 → 弃牌堆），
        /// 但"材料移出战斗"的约定不允许它再被抽到第三次进场——落进弃牌堆就静默消耗掉
        /// （带消耗词条的材料已被本体直接送进消耗堆，不用管）。
        /// </summary>
        public static async Task RemoveReplayedMaterialAsync(PlayerChoiceContext choiceContext, CardModel material, Player player)
        {
            if (material.Pile?.Type == PileType.Discard)
            {
                material.RemoveFromCurrentPile();
                await AmbushZone.ExhaustCardWithoutVisualsAsync(choiceContext, material, player);
                Log.Info($"吞噬：重放完毕，材料 {material.Id.Entry} 移出战斗");
            }
        }
    }
}
