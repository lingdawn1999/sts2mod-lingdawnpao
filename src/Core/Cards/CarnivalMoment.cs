using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻：**3 费**罕见（蓝）能力（升级 2 费）。
    /// **每回合，你每次造成冰冻后，当前回合下一次造成的冰冻层数 +1**（首次 +0、逐次递增，回合开始重置）。
    /// 结算与回合重置见 <see cref="CarnivalMomentPower" /> / <see cref="FrostbitePower.ApplyFrostAsync" />。
    /// 定位：爆发轮的滚雪球引擎——雪球连发/多段挂冰的回合里越打越疼，与「凝寒」的全程成长相互独立。
    /// <para>
    /// **沿革**：①2023-09-23 重做为"雪球挂冰 +1/+2"；②2026-09-27 重做为"雪球冰冻溅射全体"
    /// （升级全体 +1）；③**同日用户三改定稿**：溅射的"永久全局修正太猎奇"→ 回合内滚动加成，
    /// 费用 0 → **3**（升级 2，降费/数值二选一惯例）。
    /// </para>
    /// </summary>
    public sealed class CarnivalMoment : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        public CarnivalMoment()
            : base(3, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            // Amount=1 起始（无加成的标记态），每次挂冰后 +1，回合开始重置
            await PowerCmd.Apply<CarnivalMomentPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 3 → 2 费（2026-09-27 用户定案）
        }
    }
}
