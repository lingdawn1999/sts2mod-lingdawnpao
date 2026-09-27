using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 吞噬：1 费罕见（蓝）技能，[gold]消耗[/gold]——v2 吞噬合成轴的**频率闸**（设计稿第三节、第七节）。
    /// 选择[gold]伏击区域[/gold]中的两张同类型牌，合成一张合体卡
    /// （费用/伤害相加、特效拼接、词条收窄，规则见 <see cref="FusionCard" /> / <see cref="FusionEngine" />）。
    /// <para>
    /// **选牌交互**：两段式选择（复用 <see cref="AmbushZone.SelectOneAsync" /> 的 FromSimpleGrid 联机同步管线）——
    /// 第一张任选（即**基底**，决定合体卡的卡名与卡图），第二张限定与基底同类型（即燃料）。
    /// 任意一步取消则整卡空放（消耗照常）。
    /// </para>
    /// <para>
    /// **经济学地基**（设计稿）：材料是"二手的"——打出过一次才进的区域，合成给的是第二次生命，
    /// 守恒即纯赚；因此本卡的消耗词条 + 每张一次的选择成本就是全部频率闸，产物数值允许超曲线
    /// （用户 2026-09-26 定稿：机制爽感优先，平衡后置）。
    /// </para>
    /// <para>
    /// **类名沿革**：`Devour` 已被「神速」占用（它才是原名「吞噬」、2026-09-23 重做改名后的现用类），
    /// 本卡类名取 `Cannibalize`，id 同步为 `CANNIBALIZE`。
    /// </para>
    /// </summary>
    public sealed class Cannibalize : CardModel, ILingdawnpaoCard
    {
        public Cannibalize()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
        {
        }

        protected override IEnumerable<DynamicVar> CanonicalVars => Array.Empty<DynamicVar>();

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ICombatState? combat = CombatState;
            if (combat == null)
            {
                return;
            }

            // 第一张材料：区域里任意可合成牌（即基底——产物卡名卡图跟它走）
            CardModel? baseMaterial = await AmbushZone.SelectOneAsync(
                choiceContext, Owner,
                new LocString("cards", "CANNIBALIZE.pick1"),
                FusionEngine.IsFusable);
            if (baseMaterial == null)
            {
                Log.Info("吞噬：未选择基底材料，空放");
                return;
            }

            // 第二张材料：与基底同类型的其余可合成牌（即燃料）
            CardModel? fuelMaterial = await AmbushZone.SelectOneAsync(
                choiceContext, Owner,
                new LocString("cards", "CANNIBALIZE.pick2"),
                card => card != baseMaterial && card.Type == baseMaterial.Type && FusionEngine.IsFusable(card));
            if (fuelMaterial == null)
            {
                Log.Info("吞噬：未选择燃料材料，空放");
                return;
            }

            await FusionEngine.TryFuseAsync(choiceContext, Owner, combat, baseMaterial, fuelMaterial);
        }

        protected override void OnUpgrade()
        {
            // 升级降费：1 → 0。合成频率是这张卡的全部强度，费用是唯一合理的升级杠杆
            // （产物本身不可再升级，升级不可能写进产物数值；UpgradeBy 内部钳到 0）。
            EnergyCost.UpgradeBy(-1);
        }
    }
}
