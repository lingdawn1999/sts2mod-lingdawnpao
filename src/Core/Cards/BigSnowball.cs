using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 大雪球：**衍生牌**（Token），0 费[gold]攻击[/gold]，造成 1 点伤害并给予 4 层冰冻（升级 5），带[gold]消耗[/gold]。
    /// 由「大雪球！」生成。是「雪球」的加重版（一次垫更多层数，更快把目标压进冰冻处决线）。
    /// **2026-09-24 与「雪球」同步定稿**：由技能牌改为[gold]攻击牌[/gold] + 补 1 点攻击伤害；
    /// 同时**移除「雪球技巧」的群攻改写**（那张牌整张改做桥接件，`SnowballTechniquePower` 已删除），
    /// 本卡恢复为纯单体牌。因此伏击区里的大雪球现在给 +1 [gold]力量[/gold]（原为 +1 敏捷）。
    /// **2026-09-22 用户要求整体上抬**：3 层 → **4 层**（升级 4 → 5），
    /// 与「雪球」同步——同日冰冻改成"结算后层数减半"，整条雪球线一起上调（见 Snowball 的说明）。
    /// 稀有度 Token：与「雪球」同处置（不进奖励 / 商店 / 百科随机生成）。
    /// </summary>
    public sealed class BigSnowball : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录（见 Snowball 同款说明）。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/big_snowball.tres";

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(1m, ValueProp.Move),
            new PowerVar<FrostbitePower>(4m),
        ];

        public BigSnowball()
            : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target,
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(1m); // 冰冻 4 → 5（伤害不吃升级）
        }

        /// <summary>
        /// 生成 count 张大雪球加入手牌；<paramref name="upgrade" /> 为 true 时生成**升级版**（大雪球+）。
        /// 目前只有「雪场」的升级版走高一级的牌（走的是另一条路，见 SnowfieldPower），
        /// 产弹牌里「大雪球！」升级只加张数、仍是普通版（2026-09-25 用户削弱后）。
        /// </summary>
        public static async Task<List<CardModel>> CreateIntoHandAsync(Player owner, int count, ICombatState combatState, bool upgrade = false)
        {
            List<CardModel> cards = new List<CardModel>();
            if (count <= 0 || CombatManager.Instance.IsOverOrEnding)
            {
                return cards;
            }

            for (int i = 0; i < count; i++)
            {
                BigSnowball snowball = combatState.CreateCard<BigSnowball>(owner);
                if (upgrade)
                {
                    CardCmd.Upgrade(snowball);
                }

                cards.Add(snowball);
            }

            await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, owner);
            return cards;
        }
    }
}