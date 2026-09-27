using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 哈气：**1 费普通（白）技能**，单体减益，自带[gold]消耗[/gold]。
    /// 给予 **2** 层[gold]易伤[/gold]；**若该敌人当前的意图是[gold]攻击[/gold]，额外给予 2 层[gold]虚弱[/gold]**。
    /// **升级：两条一起 +1**（易伤 3 / 虚弱 3）。
    /// <para>
    /// **2026-09-24 用户定稿（第三次口径）**：① **去掉原来的"抽 1 张牌"**（保留消耗）；
    /// ② 虚弱从"无条件"改为**意图分支**。定稿理由是"1 费白技能 + 纯易伤"这条组合与本体
    /// 「战栗」Tremble（1 费白技能 · 3 易伤 · 消耗，升级 +1 易伤）**除升级项外完全同款**；
    /// 上一版靠"少 1 层易伤换 1 张抽牌"避开重复，但那样它就变成"挂易伤 + 过牌"的循环件，
    /// 乘区（易伤 +50% 伤害）被过牌带着反复回收，与战栗的设计基调（一次性减益）已经不同。
    /// </para>
    /// <para>
    /// **为什么用意图分支撑区分度**：mod 里「断腕」已经用了"意图不是攻击则击晕"的分支
    /// （见 `DeepBite`），意图判定走本体 `MonsterModel.IntendsToAttack`（内部看 `IntentType`
    /// 是否为 Attack / DeathBlow），不自研解析。本卡把同一条判定反过来用：**敌人要打你时先卸掉它的手**
    /// （虚弱 -25% 输出），敌人要上 buff / 防御时只留易伤——同一张牌在"读意图"这件事上有不同的最优出手时机，
    /// 与战栗"随时打出都一样"拉开辨识度。
    /// </para>
    /// <para>
    /// **结算顺序**：先挂易伤、再判意图挂虚弱——虚弱是条件项，放在后面不影响易伤的独立性。
    /// **消耗必须留**：易伤是稀缺乘区，本卡 1 费就能给到 2 层（升级 3 层），不消耗会成为
    /// 每回合都能复读的常驻减益；本体战栗同样自带消耗。
    /// **不是怪物的目标**（玩家/召唤物）没有 `Monster`，直接跳过虚弱分支（不报错）。
    /// </para>
    /// </summary>
    public sealed class Hiss : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<VulnerablePower>(),
            HoverTipFactory.FromPower<WeakPower>(),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new PowerVar<VulnerablePower>(2m),
            new PowerVar<WeakPower>(2m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public Hiss()
            : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target,
                DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);

            if (cardPlay.Target.Monster is not { IntendsToAttack: true })
            {
                return;
            }

            await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target,
                DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars["VulnerablePower"].UpgradeValueBy(1m); // 易伤 2 → 3
            DynamicVars["WeakPower"].UpgradeValueBy(1m);       // 虚弱 2 → 3
        }
    }
}