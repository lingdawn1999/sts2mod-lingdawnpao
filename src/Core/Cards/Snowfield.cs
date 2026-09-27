using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪场：1 费罕见（蓝）能力【v2 改造定稿】。**在你的回合开始时，将抽牌堆顶的 1 张牌
    /// 埋入[gold]伏击区域[/gold]（不占每回合入区额度）**，升级后 2 张。
    /// 皮卡丘「浴电」同构的入流引擎——区域自动进料，玩家零操作；结算在 <see cref="SnowfieldPower.AfterSideTurnStart" />。
    /// <para>
    /// **v2 沿革**：原为"回合开始把 1 张「雪球」加入手牌"（升级换「大雪球」）——雪球线整线退役后
    /// （方案 A，挂冰乘骑全空），本卡改造成伏击区的自动进料器：埋的是**你自己牌组里的真牌**，
    /// 每回合稳定 +1 力量/敏捷来源 + 给「收网」「霜牙」这类区域存量牌喂料，区域满仓时还能当烧牌成长的燃料。
    /// </para>
    /// </summary>
    public sealed class Snowfield : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<SnowfieldPower>(1m),
        ];

        public Snowfield()
            : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
            await PowerCmd.Apply<SnowfieldPower>(choiceContext, Owner.Creature,
                DynamicVars["SnowfieldPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["SnowfieldPower"].UpgradeValueBy(1m); // 每回合进料 1 张 → 2 张
        }
    }
}
