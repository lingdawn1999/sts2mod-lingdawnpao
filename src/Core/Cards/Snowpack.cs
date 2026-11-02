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
    /// 积雪：1费罕见能力、升级固有。每当一张牌加入伏击区域，获得3点格挡。
    /// 手动及效果置入均触发，带出、消耗和清空不触发。
    /// 结算入口为AmbushZone.NotifyCountChangedAsync，持续整场战斗。
    /// <para>
    /// **2026-10-28 用户调优（"收集期防御真空"对策）**：基础 2 → 3，升级位保持[gold]固有[/gold]
    /// （开局必抽，尽早挂上）——埋牌动作本身顺手起防，不用再花手牌打碎格挡。
    /// </para>
    /// </summary>
    public sealed class Snowpack : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<SnowpackPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<SnowpackPower>(3m),
        ];

        public Snowpack()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<SnowpackPower>(choiceContext, Owner.Creature,
                DynamicVars["SnowpackPower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            AddKeyword(CardKeyword.Innate);
        }
    }
}
