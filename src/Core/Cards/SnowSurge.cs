using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 叠势（2026-09-24 用户定名；照「断腕/深咬」先例**只改显示名**，内部 key `SNOW_SURGE`
    /// 与类名 `SnowSurge` 保持不变）：**1 费蓝卡攻击**。造成 6 点伤害，**本回合你每打出过一张牌，此伤害 +2**
    /// （升级：基础 6 → 8、每张加成 2 → 3）。
    /// <para>
    /// **2026-09-24 新增**（用户先定方向、再定数值）：起因是统计卡池类型配比后发现
    /// **蓝卡技能占 58%、攻击只有 25%**（本体基准大致攻/技各 40%），所以这最后一张补的是**攻击牌**；
    /// 又因为蓝卡攻击里已经覆盖了单体大伤（扑杀/锁喉/雪峰压顶）、多段随机（乱雪纷飞）、
    /// 冰冻兑现（破冰猛击）、能量转换（雪暴倾泻）、区域抵费（有备而来）、循环（循迹），
    /// 唯独没有"**本回合出牌数 → 伤害**"这条连击轴，于是它落在这里。
    /// </para>
    /// <para>
    /// **数值结构照抄本体「完美打击」（PerfectedStrike）**：`CalculationBaseVar` + `ExtraDamageVar`
    /// + `CalculatedDamageVar.WithMultiplier`，公式固定为 `基础 + 每张 × 张数`，
    /// 卡面由引擎渲染成**实时算好的伤害数字**，玩家在手牌里就能看着它随着连续出牌变大。
    /// </para>
    /// <para>
    /// **口径：只数"本回合已经结算完的出牌"**，也就是**不含正在结算的这张自己**。做法是用
    /// `CombatManager.Instance.History.CardPlaysFinished`（本体 `Fetch`、`PhantomBladesPower` 同一数据源），
    /// 而**不是** `CardPlaysStarted`——后者在出牌那一刻就已记入，会让真正打出时比卡面预览多算 1 张，
    /// 卡面就成了骗人的数字。按 owner 过滤，联机时只数自己的出牌（队友的出牌不计）。
    /// </para>
    /// <para>
    /// 结算窗口：出牌历史每回合重算（`HappenedThisTurn` 按本回合判定），所以跨回合自动归零，
    /// 不需要任何手动清零；战斗外的卡牌预览拿不到 `CombatState`，回落成基础值。
    /// 伏击区域**带出**的自动出牌同样计入（它确实是"本回合出过的一张牌"），这是刻意的：
    /// 埋牌循环铺得顺，这张牌就越疼。
    /// </para>
    /// </summary>
    public sealed class SnowSurge : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CalculationBaseVar(6m),
            new ExtraDamageVar(2m),
            new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) => CardsPlayedThisTurn(card)),
        ];

        public SnowSurge()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        /// <summary>
        /// 本回合该牌拥有者**已经结算完**的出牌数（不含正在结算的这张牌本身）。
        /// </summary>
        internal static int CardsPlayedThisTurn(CardModel card)
        {
            Player? owner = card.Owner;
            ICombatState? state = card.CombatState;
            if (owner == null || state == null)
            {
                return 0;
            }

            return CombatManager.Instance.History.CardPlaysFinished.Count(
                (CardPlayFinishedEntry e) => e.HappenedThisTurn(state) && e.CardPlay.Card.Owner == owner);
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.CalculationBase.UpgradeValueBy(2m);   // 基础伤害 6 → 8
            DynamicVars.ExtraDamage.UpgradeValueBy(1m);       // 每张加成 2 → 3
        }
    }
}