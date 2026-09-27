using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪球发射器：**2 费金卡**（Rare）技能。**打出你[gold]消耗牌堆[/gold]里所有的「雪球」与「大雪球」**，
    /// 照本体「刀刃陷阱」KnifeTrap 对消耗牌堆里的匕首的处理：这里只负责**把牌自动打出去**，
    /// 目标（首个）交给 <c>CardCmd.AutoPlay</c> 逐张传递，衍生牌自己按 `TargetType.AnyEnemy` 消费它。
    /// 升级是**质变型**：先把牌堆里的雪球**升级**再逐张打出（于是雪球变挂 3 层、大雪球变挂 5 层）。
    /// 卡面数值用 CalculatedVar 实时显示消耗牌堆里的雪球张数（同 KnifeTrap 的 CalculatedShivs）。
    /// **先取快照再遍历**：自动打出会把牌从消耗牌堆移走，直接遍历活体集合会在中途被改。
    /// <para>
    /// **费用沿革：2 费 → 1 费（2026-09-24）→ 回到 2 费（2026-09-25）**。
    /// 降 1 费的理由当时是"雪球流前期偏弱，终端降费让雪球更容易连起来"；但**条件已经变了**——
    /// 这条线后来拿到了「雪球技巧」直接把雪球**填进**区域（雪球是攻击牌，埋着每张给力量）、
    /// 「神速」「重整计划」把区域整批送进消耗牌堆，于是"0 费子弹 × 力量平台 × 免费重打"
    /// 三重乘起来：蓄势打两张时 3 张雪球 = +9 力量，每张打出 `1 + 9 = 10` 点伤害，
    /// **1 费换 30 点伤害 + 6 层挂冰**（用户 2026-09-25 实测"太变态了"）。
    /// ⚠️ **对齐本体基准**：本体「刀刃陷阱」是 **2 费 · 技能 · 稀有**，本卡同型同稀有度，
    /// 1 费等于白送 1 费。回到 2 费即回归基准；用户明确"只改费、其他不动"
    /// （升级仍是"先升级再打出"，也不限张数）。
    /// 若日后仍偏强，下一刀建议动**乘数**（例如限制张数）而不是再改费。
    /// </para>
    /// </summary>
    public sealed class SnowballLauncher : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromCard<Snowball>(upgrade: IsUpgraded),
            HoverTipFactory.FromCard<BigSnowball>(upgrade: IsUpgraded),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new CalculationBaseVar(0m),
            new CalculationExtraVar(1m),
            // 卡面战斗中显示的两个计数（2026-09-24 用户要求分开显示）：
            // CalculatedSnowballs **只数「雪球」**、CalculatedBigSnowballs 只数「大雪球」——
            // 文案写成"打出 X 张雪球和 Y 张大雪球"，两者之和就是实际会打出的张数。
            new CalculatedVar("CalculatedSnowballs").WithMultiplier(
                (CardModel card, Creature? _) => CountExhausted(card, big: false)),
            new CalculatedVar("CalculatedBigSnowballs").WithMultiplier(
                (CardModel card, Creature? _) => CountExhausted(card, big: true)),
        ];

        public SnowballLauncher()
            : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        /// <summary>消耗牌堆里的雪球（含大雪球）快照——自动打出用，顺序照牌堆。</summary>
        private static List<CardModel> ExhaustedSnowballs(CardModel card) =>
            PileType.Exhaust.GetPile(card.Owner).Cards
                .Where(c => c is Snowball || c is BigSnowball)
                .ToList();

        /// <summary>卡面计数用：只数「雪球」（big=false）或只数「大雪球」（big=true）。</summary>
        private static int CountExhausted(CardModel card, bool big) =>
            PileType.Exhaust.GetPile(card.Owner).Cards
                .Count(c => big ? c is BigSnowball : c is Snowball);

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            bool first = true;
            foreach (CardModel snowball in ExhaustedSnowballs(this))
            {
                if (IsUpgraded)
                {
                    CardCmd.Upgrade(snowball, CardPreviewStyle.None);
                }

                // skipCardPileVisuals 只在第一张时放行，其余跟随（与 KnifeTrap 一致，避免牌堆动画反复弹）
                await CardCmd.AutoPlay(choiceContext, snowball, cardPlay.Target,
                    AutoPlayType.Default, skipXCapture: false, skipCardPileVisuals: !first);
                first = false;
            }
        }

        protected override void OnUpgrade()
        {
            // 质变升级：打出的雪球/大雪球全部先升级再打出（数值由衍生牌自己的升级决定）
        }
    }
}