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
    /// 融冬：1费罕见能力，升级获得固有，费用仍为1。
    /// **由你造成的[gold]冰冻[/gold]触发时：不再造成伤害，且层数不再减半**（层数只增不减）。
    /// 结算在 <see cref="FrostbitePower.ApplyFrostAsync" /> 里按施加者身上有没有本能力分流。
    /// 定位：把冰冻从"伤害源"改造成"滚雪球资源"的总开关——
    /// 代价是放弃挂冰的即时伤害，收益是能积累层数供其他牌使用；达到处决线时仍立即处决。
    /// 只对你造成的冰冻生效（敌人给你挂冰走原本的伤害与减半）。
    /// **2026-09-22 用户改**：删掉原来"每次触发额外 +1/+2 层"的成长项（改成纯"不伤害、不衰减"），
    /// 升级位从"层数 +1 → +2"改成**给卡加[gold]固有[/gold]**；能力也随之由 Counter 变成 Single，不再带层数。
    /// **2026-09-23 升级位再改（用户定）**：固有 → **费用 1 → 0**。
    /// 理由同上（压固有浓度 + 补降费浓度，mod 升级降费 7.4% vs 本体 10.6%）。
    /// 2026-10-31：升级改回固有，保留1费启动支出，升级收益转为开局稳定性。
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
            AddKeyword(CardKeyword.Innate);
        }
    }
}
