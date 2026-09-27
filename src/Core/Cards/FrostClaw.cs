using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 霜爪：1 费罕见（蓝）技能。**分多次各挂一次冰冻**（不是一次挂多倍），
    /// 每次的层数 = 1 + 「你[gold]伏击区域[/gold]中的牌数 × 每张加成」（每张固定 +1）。
    /// 与「雪崩」同为"吃伏击区存量"的牌，但方向相反：雪崩把区域当倍率，霜爪把区域当即时层数。
    /// **分多次是刻意的**：挂冰是"每挂一次结算一段伤害并 -2 层"，所以两次小量挂冰
    /// 比一次大量更不容易被 -2 层吃掉（区域铺满时收益差得最多）。
    /// 区域为空时它只是 1 费挂 2 层（=两次 1 层），是下限。
    /// **2026-09-22 用户削弱**：升级从"每张 +2"改成"**段数 2 → 3**"（每张 +1 不变）。
    /// 原因：+2 会让满区域（3 张）单次就挂 7 层，两段叠出 **19 点伤害 + 留 10 层**，1 费太猛；
    /// 改成加段数后，第二/第三段吃到的存量小得多（满区域 3 段 = 18 点伤 + 留 6 层），
    /// **单段更小、残留更少**，整体是削，而且没破坏"多段"这个刻意的形状。
    /// **段数必须走 DynamicVar**（不能写死常量）：升级要改它，卡面 `{Repeat:diff()}` 得跟着变。
    /// </summary>
    public sealed class FrostClaw : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<FrostbitePower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<FrostbitePower>(1m),
            new RepeatVar(2),
            new DynamicVar("AmbushBonus", 1m),
        ];

        public FrostClaw()
            : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        /// <summary>每次挂冰的层数：基础值 + 区域牌数 × 每张加成。</summary>
        private decimal StacksPerHit =>
            DynamicVars["FrostbitePower"].BaseValue
            + AmbushZone.For(Owner).Count * DynamicVars["AmbushBonus"].BaseValue;

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            // 多段挂冰：本卡一次打出会挂 `Repeat` 次（基础 2、升级 3），但**「雪隐」的格挡回调只该给一次**
            // （用户 2026-09-21 定案"每次结算只给一次"），所以整段走批量入口。
            // 目标中途死亡由批量方法内部的 IsAlive 判定跳过，与原 `break` 行为一致。
            List<Creature> hits = new List<Creature>();
            for (int i = 0; i < DynamicVars.Repeat.IntValue; i++)
            {
                hits.Add(cardPlay.Target);
            }

            await FrostbitePower.ApplyFrostToAllAsync(choiceContext, hits, StacksPerHit, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Repeat.UpgradeValueBy(1m); // 段数 2 → 3（每张 +1 不变）
        }
    }
}