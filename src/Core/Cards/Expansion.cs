using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 扩张：0费罕见技能，消耗。伏击区域上限+1（升级+2）。
    /// 无需区域已满；技能类型不触发首次使用能力牌的额外扩容。
    /// </summary>
    public sealed class Expansion : CardModel, ILingdawnpaoCard
    {
        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new RepeatVar(1),
        ];

        public Expansion()
            : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            // 统一扩容入口保留区域上限9的限制。
            await AmbushZone.AddCapacityAsync(choiceContext, Owner, DynamicVars.Repeat.IntValue, "扩张");
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Repeat.UpgradeValueBy(1m); // 上限 +1 → +2
        }
    }
}
