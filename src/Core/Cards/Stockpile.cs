using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 蓄能：**0 费普通（白卡）技能**（通用/运转轴），**获得 1 点能量，自带[gold]消耗[/gold]**
    /// （升级：能量 1 → 2）。**当回合结算**，不亏节奏——这是本 mod 唯一一张"无条件、不吃当回合节奏"的加费件。
    /// <para>
    /// **2026-09-23 用户重做（原为 1 费 / 下回合获得 2 点能量）**：老版本是本体「备战」Outmaneuver 的直搬，
    /// 用户判定"**作为加费卡非常非常劣质**"——它当回合要**净亏 1 费和一张牌**，换来的只是"下回合多 2 费"，
    /// 总量净 +1 且延后一回合，等于把节奏白让出去。新版采纳"**0 费才算稳定加费**"的口径：
    /// 直接收编原「雪原疾行」的效果，**把加费件下沉到白卡**。
    /// </para>
    /// <para>
    /// **为什么必须自带「消耗」**：0 费换 1 点能量本身就是净赚 1 费，不设限就能反复白嫖
    /// （甚至在没有费用压力时当"过牌/缩牌组"用），所以用一次性把它压住。这也是它可以留在**白卡**的原因——
    /// 同类更强的档位见本体「肾上腺素」Adrenaline（0 费、+1 能量、**抽 2 张**、消耗），它明显在更高的稀有度档。
    /// </para>
    /// <para>
    /// **与「雪原疾行」的分工（2026-09-23 两张互换）**：加费件下沉白卡后，蓝卡「雪原疾行」腾出来重做成了
    /// **稳定过牌件**（1 费抽 2）。两条运转轴各归其位：**白卡给费、蓝卡给牌**——
    /// 用户 2026-09-23 的诊断是"缺乏稳定加费卡和稳定过牌卡"，这两张就是补那两个洞的。
    /// </para>
    /// </summary>
    public sealed class Stockpile : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            EnergyHoverTip,
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new EnergyVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

        public Stockpile()
            : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Energy.UpgradeValueBy(1m); // 1 → 2
        }
    }
}