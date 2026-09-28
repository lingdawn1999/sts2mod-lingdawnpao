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
    /// 凝寒：**3 费**金卡（Rare）能力（升级 2 费）。
    /// **当你造成冰冻时，本回合下次造成的冰冻层数+1**（首次 +0、逐次递增，回合开始重置）。
    /// **对群分别生效**：批量挂冰里每个敌人各吃一次当前加成、各推进一次计数（2026-09-28 用户追加定稿）。
    /// 结算与回合重置见 <see cref="FrostIntensifyPower" /> / <see cref="FrostbitePower.ApplyFrostAsync" />。
    /// 定位：爆发轮的滚雪球引擎——雪球连发/多段挂冰的回合里越打越疼。
    /// <para>
    /// **沿革**：原为"每次挂冰层数 +1"（0 费）→ 2026-09-24 升级定为纯数值 +1 → +2；
    /// 2026-09-27 重做为成长型（第 n 次挂冰 +n）+ 加费 1；
    /// **2026-09-28 用户定稿与「狂欢时刻」对调**：金卡拿**回合内滚动**（3 费/升级 2 费），
    /// 成长型随对调废弃——狂欢时刻回归雪球群攻线（见 CarnivalMoment）。
    /// </para>
    /// </summary>
    public sealed class FrostIntensify : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        public FrostIntensify()
            : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            // Amount=1 起始（无加成的标记态），每次挂冰后 +1，回合开始重置
            await PowerCmd.Apply<FrostIntensifyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 3 → 2 费（2026-09-28 用户定稿，对调后沿用降费惯例）
        }
    }
}
