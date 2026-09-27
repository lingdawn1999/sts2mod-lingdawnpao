using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 张网以待：伏击区域上限 +2（升级 +3）。1 费稀有（金卡）。
    /// 与「扩张」（区满才生效）互补：本卡无条件扩容，越早打越值钱。
    /// 只加上限不补当前埋牌数；战斗结束随区域状态一并清零。
    /// </summary>
    public sealed class WidenedNet : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(2),
        ];

        public WidenedNet()
            : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            AmbushZoneState zone = AmbushZone.For(Owner);
            // 给 AmbushCapacityPower 叠层加成上限（替代直接改字段，让上限可视化与统一管理）
            var creature = Owner.Creature;
            int n = DynamicVars.Repeat.IntValue;
            await PowerCmd.Apply<AmbushCapacityPower>(choiceContext, creature, n, creature, null);
            Log.Info($"伏击区域：上限提升至 {zone.Capacity}（张网以待）");
            Owner.GetRelic<FrostFang>()?.Flash();
            AmbushZoneUi.Refresh();
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Repeat.UpgradeValueBy(1m);
        }
    }
}
