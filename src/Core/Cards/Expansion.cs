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
    /// 扩张：0 费罕见能力。仅有在伏击区域**已满**时才生效，使上限 +1（升级 +2）。
    /// 与「张网以待」（无条件 +2 上限、金卡）互补：本卡 0 费、抽到即可随手打，
    /// 区域满时白赚一格；未满打出不消耗费用、只是不生效，不会造成亏损。
    /// </summary>
    public sealed class Expansion : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(1),
        ];

        public Expansion()
            : base(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            AmbushZoneState zone = AmbushZone.For(Owner);

            if (!zone.IsFull)
            {
                Log.Info("伏击区域：未满，扩张不生效");
                return;
            }

            // 给 AmbushCapacityPower 叠层加成上限（替代直接改字段，让上限可视化与统一管理）
            var creature = Owner.Creature;
            int n = DynamicVars.Repeat.IntValue;
            await PowerCmd.Apply<AmbushCapacityPower>(choiceContext, creature, n, creature, null);
            Log.Info($"伏击区域：已满 → 上限提升至 {zone.Capacity}（扩张）");
            Owner.GetRelic<FrostFang>()?.Flash();
            AmbushZoneUi.Refresh();
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Repeat.UpgradeValueBy(1m); // 上限 +1 → +2（2026-09-24 定案：基础保持 1，升级 +1）
        }
    }
}