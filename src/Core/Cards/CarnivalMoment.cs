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
    /// 狂欢时刻：**0 费**罕见（蓝）能力。**「雪球」与「大雪球」造成的冰冻改为溅射全体敌人**
    /// （含原目标，全体同一层数）；升级：溅射的冰冻层数 **+1**。
    /// 结算集中在 <see cref="FrostbitePower.ApplyFrostAsync" />：cardSource 是雪球/大雪球时改走
    /// 批量入口对全体敌人挂冰——「雪球发射器」「打雪仗」「雪场」这些**自动打出雪球**的来源同样吃溅射。
    /// <para>
    /// **沿革**：2026-09-23 重做为"雪球挂冰 +1/+2"；**2026-09-27 用户评定为烂卡（与凝寒同质化、
    /// 加法放大器太平）→ 重做为溅射**：从"放大器"变成雪球线的 AOE 质变位，与凝寒的成长加成解耦。
    /// </para>
    /// </summary>
    public sealed class CarnivalMoment : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(),
            HoverTipFactory.FromCard<BigSnowball>(),
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        public CarnivalMoment()
            : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            // Amount=1 为溅射开关（层数加成 = Amount-1 = 0）；升级 2 = 溅射全体 +1 层
            await PowerCmd.Apply<CarnivalMomentPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["CarnivalMomentPower"].UpgradeValueBy(1m); // 溅射加成 0 → +1（层数 1 → 2）
        }
    }
}
