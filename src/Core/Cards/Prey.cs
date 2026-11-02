using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 猎物：「捕猎」生成的 Token 衍生攻击，基础效果生成到伏击区域，埋伏效果生成到手牌。
    /// 0 费 **1 伤** + 抽 1 张牌，自带消耗（带出结算后进消耗堆，不进牌组循环）；
    /// **升级：伤害 1 → 3**（抽牌数固定 1，不再随升级增加）。
    /// 战斗结束仍埋着的由 AmbushZone.ReturnBuriedCardsAsync 按 Token 稀有度直接销毁。
    /// <para>
    /// **2026-09-25 用户削弱**：原为 0 费 3 伤 + 抽 1（升级抽 2）。改为"1 伤 / 升级 3 伤 + 固定抽 1"——
    /// 「捕猎」一次能铺 3~5 张本牌，0 费 3 伤 ×N 且张张抽牌，等于把"铺场"直接换成了成吨的伤害与手牌；
    /// 现在单张只值 1 点伤害，升级位也从"抽牌数"挪到"伤害"上（一次铺多张时伤害更直观）。
    /// </para>
    /// </summary>
    public sealed class Prey : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/prey.tres";
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(1m, ValueProp.Move),
            new CardsVar(1),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

        public Prey()
            : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }

        /// <summary>捕猎及十面埋伏共用：生成一张猎物到手牌，按原始埋伏的升级状态生成。</summary>
        internal static async Task CreateIntoHandAsync(Player owner, ICombatState combatState, bool upgrade)
        {
            if (!AmbushZone.CanContinueCombat(owner)) return;
            Prey prey = combatState.CreateCard<Prey>(owner);
            if (upgrade) CardCmd.Upgrade(prey);
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { prey }, PileType.Hand, owner);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(2m); // 伤害 1 → 3（抽牌固定 1 张）
        }
    }
}