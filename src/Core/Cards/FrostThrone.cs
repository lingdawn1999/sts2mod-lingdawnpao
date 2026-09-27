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
    /// 冰封王座：**0 费**罕见（蓝）能力。**不带任何词条**（2026-09-23 用户去掉[gold]固有[/gold]）。
    /// **每回合第一次**能量不足以支付某张牌时，该牌耗能为 0、仍可正常打出；以此法打出的牌，
    /// **每差 1 点能量**使**你自己**获得 **3** 层[gold]冰冻[/gold]（**升级后降到 2**）。
    /// 定位：冰冻线的"拿命换节奏"引擎——手牌打空、能量见底时反手把高费牌倾泻出去，
    /// 代价是自伤（冰冻在自身立即结算等量伤害，**可被格挡**）与"生命值低于层数即死"的处决风险。
    /// <para>
    /// **2026-09-24 用户两改**：① 加「每回合第一次」阀门（原为当回合无限次，见 `FrostThronePower`）；
    /// ② 自伤 4/3 → **3/2**、口径由"每 1 点原始耗能"改为"每**差** 1 点能量"。
    /// 改因：同日「神速」脱绑冰冻改绑伏击，冰冻线的自伤来源少了一条，用户判定代价偏重
    /// （原话"冰封王座可以限降低代价"）。
    /// </para>
    /// 费用沿革：1 → 0（2026-09-20"代价那么大，干脆加强到 0 费"）→ 回到 1 费并改自带固有
    /// （2026-09-21：固有保证它起手就在，1 费的"入场费"由固有能力抵消）→ **2026-09-23 用户再改回 0 费**
    /// （判断 1 费的入场费加上自伤代价双重收费太重，直接免掉费用、只留自伤作为价码）。
    /// **2026-09-23 去掉固有**：当初加固有就是为了"抵消 1 费的入场费"，改回 0 费后这个理由已经失效
    /// （0 费牌随时能打，固有的边际价值最低）；同时本体 13 张带固有的能力牌**全部是"升级给"**、
    /// 没有一张基础自带 → 去掉它也顺带回归了本体规律。
    /// **升级 = 只减自伤（3 → 2 层），费用固定 0 费不降**（用户 2026-09-21 二次定案："冰封王座升级不再减费"）。
    /// 层数是**代价**，降层数 = 减轻自伤 = 加强，所以敲过之后唯一的收益就是"不痛了"。
    /// </summary>
    public sealed class FrostThrone : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("FrostPerEnergy", 3m),
        ];

        public FrostThrone()
            : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            FrostThronePower? power = await PowerCmd.Apply<FrostThronePower>(
                choiceContext, Owner.Creature, 1m, Owner.Creature, this);
            // 层数（每差 1 点能量换来的自伤冰层）写在 power 的 Amount 上：打出时覆盖写入，不叠加。
            // 这样 power 文案里的 {Amount} 与卡面 {FrostPerEnergy} 永远同源，不会各说各话。
            power?.SetAmount((int)DynamicVars["FrostPerEnergy"].BaseValue);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            // 只减自伤，不降费（用户 2026-09-21 定案：一直 1 费）。
            DynamicVars["FrostPerEnergy"].UpgradeValueBy(-1m); // 3 → 2
        }
    }
}