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
    /// 雪球：**衍生牌**（Token），0 费[gold]攻击[/gold]，造成 1 点伤害并给予 2 层冰冻（升级 3），带[gold]消耗[/gold]。
    /// 冰球线的"子弹"——由「雪球保护」「打雪仗」「雪场」生成到手牌，「大雪球！」生成的是加强版「大雪球」；
    /// 「雪球技巧」则把它们**直接埋进**[gold]伏击区域[/gold]（不进手牌，存起来延后免费打出）。
    /// <para>
    /// **2026-09-24 用户定稿：由技能牌改为[gold]攻击牌[/gold]**，并补 1 点攻击伤害
    /// （原为"纯挂冰技能"，见下方沿革）。改动带来两处连锁，都是有意的：
    /// ① **伏击区的类型加成换了**——区域按牌类型给加成（攻击→[gold]力量[/gold]、技能→[gold]敏捷[/gold]），
    ///    所以被埋进区域的雪球现在给 **+1 力量**（原为 +1 敏捷），「雪球技巧」填满区域时就是一座力量平台；
    /// ② **1 点伤害吃[gold]力量[/gold]**（`ValueProp.Move` 口径），上面那座力量平台会把这 1 点撑起来；
    ///    挂冰段落仍是 `Unpowered`（不吃力量），与全 mod 的冰冻口径一致。
    /// 另外它现在也是"打出攻击牌"，会触发「棘背龙形态」这类"每打出一张攻击牌"的钩子。
    /// </para>
    /// <para>
    /// **沿革**：2026-09-22 用户要求整体上抬：1 层 → **2 层**（升级 2 → 3）。
    /// 起因是同日冰冻改成"结算后**层数减半**"（见 FrostbitePower.LayersRetainedDivisor）：
    /// 1 层挂冰在新规则下 = 打 1 点、减半直接归 0、零残留，这条"子弹线"几乎失效，
    /// 所以把它抬到 2 层（打 2 点、留 1 层）重新有存在感。
    /// **同日也移除了「雪球技巧」的群攻改写**（用户判定那张牌是"蠢卡"，整张改做桥接件，
    /// `SnowballTechniquePower` 随之删除）——雪球与大雪球恢复为纯单体牌。
    /// 稀有度 Token：已登记进本体的 `TokenCardPool`（见 ModInitializer 的说明），
    /// 卡面路径另行钉死到雪豹目录（同款见下方 PortraitPath）。
    /// </para>
    /// </summary>
    public sealed class Snowball : CardModel, ILingdawnpaoCard
    {
        /// <summary>卡面钉死在雪豹目录：本卡挂的是 TokenCardPool（Pool.Title="token"），
        /// 本体默认路径会去找 `atlases/card_atlas.sprites/token/`（我们没那个目录）→ 变占位图。</summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/snowball.tres";

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(1m, ValueProp.Move),
            new PowerVar<FrostbitePower>(2m),
        ];

        public Snowball()
            : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 顺序：先打那 1 点攻击伤害，再挂冰（挂冰自带一次等同层数的即时结算）
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            await FrostbitePower.ApplyFrostAsync(choiceContext, cardPlay.Target,
                DynamicVars["FrostbitePower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["FrostbitePower"].UpgradeValueBy(1m); // 冰冻 2 → 3（伤害不吃升级）
        }

        /// <summary>
        /// 生成 count 张雪球加入手牌；<paramref name="upgrade" /> 为 true 时生成**升级版**（即"雪球+"）。
        /// 「雪球保护」升级后给的就是升级版雪球，「打雪仗」升级后给的 4 张也全是升级版。
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
                Snowball snowball = combatState.CreateCard<Snowball>(owner);
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