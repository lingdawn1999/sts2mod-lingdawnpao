using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 暗袭：「设伏」生成的 Token 衍生牌，只能出现在伏击区域、被带出时免费打出。
    /// 0 费 5 伤攻击，自带消耗（带出打出后进消耗堆，不进牌组循环）；战斗结束仍埋着的
    /// 由 AmbushZone.ReturnBuriedCardsAsync 按 Token 稀有度直接销毁。
    /// </summary>
    public sealed class AmbushStrike : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/ambush_strike.tres";
        protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(5m, ValueProp.Move),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

        public AmbushStrike()
            : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }
}
