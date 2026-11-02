using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 大雪球！：1费罕见技能，将2张大雪球加入手牌。
    /// 升级生成大雪球+，数量保持2张；本卡不消耗，可重复产出。
    /// </summary>
    public sealed class BigSnowballVolley : CardModel, ILingdawnpaoCard
    {
        /// <summary>升级前后均生成2张。</summary>
        private const int BigSnowballCount = 2;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<BigSnowball>(upgrade: IsUpgraded),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("Snowballs", BigSnowballCount),
        ];

        public BigSnowballVolley()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await BigSnowball.CreateIntoHandAsync(Owner, (int)DynamicVars["Snowballs"].BaseValue, CombatState!,
                upgrade: IsUpgraded);
        }
    }
}
