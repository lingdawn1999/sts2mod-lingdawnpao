using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 狂欢时刻：**1 费**罕见（蓝）能力（升级：加成 +1 → +2）。
    /// **你的雪球与大雪球造成的冰冻层数+1，并溅射给所有敌人**——雪球线群攻件。
    /// 静态加成结算在 <see cref="FrostbitePower.ApplyFrostAsync" />（不设批次门槛），
    /// 溅射在 <see cref="Snowball.OnPlay" /> / <see cref="BigSnowball.OnPlay" />（有本能力时改走批量挂冰入口）。
    /// <para>
    /// **沿革**：①2023-09-23 重做为"雪球挂冰 +1/+2"；②2026-09-27 重做为"雪球冰冻溅射全体"；
    /// ③同日三改为"回合内滚动加成"（3 费）；
    /// **④2026-09-28 用户定稿与「凝寒」对调**：滚动给金卡，本卡回归**雪球群攻**方向——
    /// 溅射与"+1 冰冻"合并定价 **1 费**（升级 +2）。②的"永久全局修正太猎奇"教训保留：
    /// 加成只认雪球系 cardSource、只加层数，不碰全局伤害修正。
    /// </para>
    /// </summary>
    public sealed class CarnivalMoment : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
            HoverTipFactory.FromCard<Snowball>(),
            HoverTipFactory.FromCard<BigSnowball>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<CarnivalMomentPower>(1m),
        ];

        public CarnivalMoment()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<CarnivalMomentPower>(choiceContext, Owner.Creature,
                DynamicVars["CarnivalMomentPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["CarnivalMomentPower"].UpgradeValueBy(1m); // +1 → +2（2026-09-28 用户定稿）
        }
    }
}
