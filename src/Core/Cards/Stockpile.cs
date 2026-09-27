using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 蓄能：**1 费普通（白卡）技能**，**选择 1 张手牌加入[gold]伏击区域[/gold]，下回合获得 2 点能量**
    /// （升级：能量 2 → 3，并**去掉[gold]消耗[/gold]**）。能量的"下回合"结算复用本体「备战」
    /// Outmaneuver 的 <c>EnergyNextTurnPower</c>（零新增视觉/联机成本，层数可叠加）。
    /// <para>
    /// **2026-09-27 用户重做（三进宫，记全沿革防走回头路）**：
    /// ① 初版 = 本体「备战」直搬（1 费 / 下回合 2 能量）——2026-09-23 用户判定"作为加费卡非常劣质"
    ///   （当回合净亏 1 费 + 一张牌），改成 0 费当回合 +1 能量 + 消耗；
    /// ② 2026-09-23 第二轮：收编原「雪原疾行」的"白卡给费"定位（白卡给费、蓝卡给牌）；
    /// ③ 2026-09-27（上午）：回到"1 费 / 下回合 2 能量"的老骨架，挂上"选 1 张手牌入区"作为卖点，
    ///    升级 = 能量+1 且去消耗；
    /// ④ **本版（2026-09-27 用户定稿）**：**基础就去掉[gold]消耗[/gold]**（升级位只剩能量 2 → 3）——
    ///    可重复使用后它是稳定的埋牌 + 下回合回费件，基础可复用更符合白卡定位。
    /// </para>
    /// <para>
    /// **埋入路径**：照暗杀者遗物的手牌埋入（<see cref="AmbushZone.BuryAsync" />，`spendResources: false`，
    /// 手牌 holder 释放 + 飞入动画同一套管线）；区域已满 / 冷却时埋不进，选中的牌留在手里
    /// （照「设伏」既有约定），能量照常结算。选牌走 <c>CardSelectCmd.FromHand</c>：姿态牌被
    /// <see cref="CardSelectCmd_FromHand_AmbushStanceFilterPatch" /> 的过滤自动排除。
    /// </para>
    /// </summary>
    public sealed class Stockpile : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            EnergyHoverTip,
            HoverTipFactory.FromPower<EnergyNextTurnPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new EnergyVar(2),
        ];

        // 基础不带消耗（2026-09-27 用户定稿；升级位只剩能量 +1）

        public Stockpile()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            CardModel? picked = (await CardSelectCmd.FromHand(
                context: choiceContext,
                player: Owner,
                prefs: new CardSelectorPrefs(SelectionScreenPrompt, 1),
                filter: null,
                source: this)).FirstOrDefault();

            if (picked != null)
            {
                await AmbushZone.BuryAsync(choiceContext, picked, Owner, spendResources: false);
            }

            // 下回合能量：复用本体「备战」Outmaneuver 的 EnergyNextTurnPower（层数叠加）
            await PowerCmd.Apply<EnergyNextTurnPower>(
                choiceContext, Owner.Creature, DynamicVars.Energy.IntValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Energy.UpgradeValueBy(1m); // 下回合 2 → 3（消耗已从基础移除）
        }
    }
}
