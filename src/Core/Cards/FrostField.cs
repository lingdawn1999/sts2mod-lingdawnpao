using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜原：1费普通技能，给予所有敌人4（6）层冰冻。
    /// 冰冻线的 AOE 挂冰源：单体挂冰（覆霜/冰冻打击/豹咬）在多敌人战斗里层数推进太慢，
    /// 这张负责一次性把全场层数垫起来，与「冰封千里」「霜之护盾」这类群伤/群收益咬合。
    /// 逐敌调用 ApplyFrostAsync（每只敌人的挂冰伤害各自独立结算），不是一次群发。
    /// **数值沿革**：初版 3/5 → 2026-09-21 上调 4/6 → **2026-09-25 用户下调回 3/5**（升级增量仍为 +2）。
    /// 2026-10-27：按用户定案回调至4（6），补充未成型时的群体挂冰能力。
    /// </summary>
    public sealed class FrostField : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(4m),
        ];

        public FrostField()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 先取快照（挂冰伤害可能打死敌人）；走**批量入口**——「雪隐」的格挡回调整批只触发一次，
            // 不随敌人数放大（用户 2026-09-21 定案）
            await FrostbitePower.ApplyFrostToAllAsync(choiceContext, CombatState!.HittableEnemies.ToList(),
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(2m); // 冰冻 4 → 6
        }
    }
}
