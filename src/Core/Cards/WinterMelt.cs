using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 融冬：1 费罕见（蓝）能力。**升级：费用 1 → 0**（2026-09-23 用户改，原为"获得[gold]固有[/gold]"）。
    /// **由你造成的[gold]冰冻[/gold]触发时：不再造成伤害，且层数不再减半**（层数只增不减）。
    /// 结算在 <see cref="FrostbitePower.ApplyFrostAsync" /> 里按施加者身上有没有本能力分流。
    /// 定位：把冰冻从"伤害源"改造成"滚雪球资源"的总开关——
    /// 代价是彻底放弃挂冰的即时伤害与处决线推进，收益是「雪崩」「寒灾」「霜天」这些吃层数的牌层数只增不减。
    /// 只对你造成的冰冻生效（敌人给你挂冰走原本的伤害与减半）。
    /// **2026-09-22 用户改**：删掉原来"每次触发额外 +1/+2 层"的成长项（改成纯"不伤害、不衰减"），
    /// 升级位从"层数 +1 → +2"改成**给卡加[gold]固有[/gold]**；能力也随之由 Counter 变成 Single，不再带层数。
    /// **2026-09-23 升级位再改（用户定）**：固有 → **费用 1 → 0**。
    /// 理由同上（压固有浓度 + 补降费浓度，mod 升级降费 7.4% vs 本体 10.6%）。
    /// ⚠️ **副作用（观察项）**：0 费常驻总开关（本体 0 费能力只有 Panache / Neurosurge 两张），
    /// 若实测偏强，**第一个该动的是费用**（退回 1 费），不要动效果。
    /// </summary>
    public sealed class WinterMelt : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        public WinterMelt()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            // Single 形态的能力：层数无意义，固定写 1
            await PowerCmd.Apply<WinterMeltPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 费 → 0 费（2026-09-23 用户改，原为"获得固有"）
        }
    }
}