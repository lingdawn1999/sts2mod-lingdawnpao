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
    /// 凝寒：**1 费**金卡（Rare）能力（2026-09-27 用户定案加费 0 → 1）。
    /// **你每次造成冰冻时，本次施加的冰冻层数额外 + 你本场战斗造成冰冻的次数**
    /// （含本次：第 1 次 +1、第 2 次 +2、第 n 次 +n，越打越疼）。
    /// 实现见 <see cref="FrostbitePower.ApplyFrostAsync" />：加成 = 能力当前层数，
    /// 每次顶层挂冰结算后 +1（批量溅射整批只计 1 次）；打出时垫 1 层即"首次 +1"。
    /// 结算在施加前加算，同时抬高"这一次的挂冰伤害"与"残留层数"——整条冰冻线的全局放大器。
    /// 冰封王座自挂（applier=null）不计数、不加成。
    /// <para>
    /// **沿革**：原为"每次挂冰层数 +1"（0 费）；2026-09-24 升级定为纯数值 +1 → +2；
    /// **2026-09-27 用户评定金卡一般般 → 重做为成长型 + 加费 1**（升级：降费 1 → 0，降费/数值二选一惯例）。
    /// </para>
    /// </summary>
    public sealed class FrostIntensify : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        public FrostIntensify()
            : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<FrostIntensifyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            EnergyCost.UpgradeBy(-1); // 1 → 0 费（2026-09-27 用户定案，加费后升级降费）
        }
    }
}
