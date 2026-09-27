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
    /// 雪场：1 费罕见（蓝）能力。**在你的回合开始时，把 1 张「雪球」加入手牌**。
    /// 冰球线的自动供弹器，结算在 <see cref="SnowfieldPower.AfterSideTurnStart" />。
    /// **2026-09-25 用户改升级位**：原为"每回合 1 张 → 2 张"，改成"**雪球 → 大雪球**"
    /// （用户原话"雪场升级改成给大雪球"）——张数固定 1 张，升级买的是每张的质量
    /// （大雪球是雪球的强化版，见 BigSnowball）。
    /// 定位：与「霜天」（回合开始自动挂冰）并列的两个"回合引擎"——
    /// 霜天直接把层数推进，雪场改成给玩家"子弹"，需要玩家自己花费用打出去。
    /// </summary>
    public sealed class Snowfield : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            // 升级后供弹换的是大雪球，只提示真正会给的那张（两张都挂太吵）
            IsUpgraded ? HoverTipFactory.FromCard<BigSnowball>() : HoverTipFactory.FromCard<Snowball>(),
        ];

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

            // 升级：供弹从「雪球」换成「大雪球」（旗标记在能力实例上，每回合读一次）
            if (IsUpgraded && Owner.Creature.GetPower<SnowfieldPower>() is { } power)
            {
                power.SpawnsBig = true;
            }
        }

        protected override void OnUpgrade()
        {
            // 质变升级：供弹的雪球换成大雪球（旗标在实际挂能力时写入，见 OnPlay）
        }
    }
}