using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 连扑：1 费金卡技能（放大器档，笔记 #7 那张）。本回合内，从伏击区域带出的**下 1 张**牌
    /// 会额外打出一遍（升级 2 张）；这些牌**结算后消耗**。
    /// 阀门是"张数上限 + 结算后消耗"双保险（用户定案）：因为区域可以主动堆满、且设伏生成的暗袭
    /// 本身就是 Token（消耗它们零代价），只靠"消耗"当阀门在 token 线上是失效的。
    /// **升级只加张数，绝不动阀门**（对标本体 OneTwoPunch / Burst 的升级方式）。
    /// <para>
    /// **2026-09-24 用户定案：2 费 → 1 费，张数 2/3 → 1/2。** 原档位（2 费换 2 张翻倍）要求
    /// 一回合里攒够 2 张带出牌才回本，而它自带"结算后消耗"，实际上先把区域清空、门槛不低；
    /// 降到 1 费、只保底 1 张之后，它从"必须先铺好两张的兑现牌"变成**随手可配的带出放大器**，
    /// 与本流派"来回变动区域"的节奏绑得更自然。代价侧不变（结算后消耗）。
    /// </para>
    /// </summary>
    public sealed class ChainPounce : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/chain_pounce.tres";

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<ChainPouncePower>(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<ChainPouncePower>(1m),
        ];

        public ChainPounce()
            : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
            await PowerCmd.Apply<ChainPouncePower>(choiceContext, Owner.Creature,
                DynamicVars["ChainPouncePower"].BaseValue, Owner.Creature, this);
            Owner.GetRelic<FrostFang>()?.Flash();
        }

        protected override void OnUpgrade()
        {
            DynamicVars["ChainPouncePower"].UpgradeValueBy(1m); // 1 张 → 2 张
        }
    }
}