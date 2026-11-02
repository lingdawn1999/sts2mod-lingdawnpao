using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 断腕（2026-09-23 由「深咬」改名，**只改显示名，内部 key `DEEP_BITE` 与类名保持不变**）：
    /// **0 费金卡攻击，自带[gold]消耗[/gold]**。造成 6 点伤害（升级 +3 → 9），并给予 1 层[gold]虚弱[/gold]
    /// （升级 +1 → 2 层）。**埋伏**（跨回合从伏击区域带出打出）：若目标**当前的意图不是攻击**，则**击晕**它。
    /// <para>
    /// **2026-09-24 用户爆改成攻击牌**（原话："深喉感觉也可以爆改到攻击卡"）：原为**技能**（只挂虚弱 + 埋伏击晕），
    /// 改成攻击后既补了金卡的攻击浓度，也让它**空放时不再亏模**——1 费时代打出去只有 1 层虚弱"太亏模"，
    /// 现在即使没走埋伏段，0 费打 6 伤 + 1 虚弱也是正常 0 费件的水平。
    /// **数值定案**：0 费、伤害 6（升级 9）、虚弱 1（升级 2）。
    /// </para>
    /// <para>
    /// **费用沿革**：1 费 → 0 费（2026-09-24 用户："不然直接打出的时候太亏模"）。
    /// 注意**埋入也照付这张牌的费用**，所以 0 费后"埋→带出"的循环成本同步降为 0，
    /// 但**自带消耗**仍把循环限制成一次性（见下方消耗的理由）。
    /// </para>
    /// <para>
    /// **2026-09-23 用户重做**。原效果是"从伏击区带出的牌，对有易伤的敌人额外 +50%（升级 +100%）"，
    /// 用户判定"没啥用"——**根因是易伤覆盖率太低**：本 mod 挂易伤的只有「锁喉」「哈气」等极少几张，
    /// 条件几乎不成立。重做后彻底换掉"易伤乘区"这条轴，改成**控制**：
    /// 打弱化 + 埋伏时按敌人意图决定是否直接罚站一回合。
    /// </para>
    /// <para>
    /// **为什么是"意图不为攻击"**：这是玩家**读得懂、能提前规划**的信息——把埋伏的时机判断
    /// 交给"看敌人头上那个图标"，而不是让玩家去背怪物的行动表。敌人准备强化/防御/上负面时
    /// 埋进去、下回合带出，就能白赚一个回合的节奏，正好补上本流派"埋→带出"最缺的防守面。
    /// </para>
    /// <para>
    /// **自带[gold]消耗[/gold]（用户同日追加）**：埋伏线的核心是"同一张牌反复埋→带出"的循环，
    /// 而击晕是**跳过敌方整个回合**级别的节奏收益，允许无限循环太强，故打完即消耗——一次性的"打断"。
    /// </para>
    /// <para>
    /// **实现照本体「哨声」Whistle**（3 费先古攻击：造成伤害 + <c>CreatureCmd.Stun</c>）：
    /// ① 意图判定用本体 `MonsterModel.IntendsToAttack`（内部看 `IntentType` 是否为 Attack / DeathBlow），
    /// 不自己解析意图类型；② 击晕用 `CreatureCmd.Stun(target)`（自带 `NStunnedVfx` 眩晕特效）。
    /// **不是怪物的目标**（玩家/召唤物）没有 `Monster`，直接跳过。
    /// </para>
    /// <para>
    /// ⚠️ **击晕**不进 `AmbushTriggerCount` 的循环：伏兵让埋伏段重复触发对"施加层数"有意义，
    /// 但击晕是布尔状态、重复调用没有额外收益（`IntendsToAttack` 在击晕后已不成立），故只结算一次。
    /// **虚弱是基础效果**，不随埋伏重复触发（照本流派"埋伏分支才吃伏兵"的约定）。
    /// </para>
    /// </summary>
    public sealed class DeepBite : CardModel, IAmbushBonusCard
    {
        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<WeakPower>(),
            StunIntent.GetStaticHoverTip(),
            new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description")),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(6m, ValueProp.Move),
            new PowerVar<WeakPower>(1m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public DeepBite()
            : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);

            if (!AmbushZone.IsVeteranAmbush(this)
                || cardPlay.Target.Monster is not { } monster
                || monster.IntendsToAttack)
            {
                return;
            }

            Log.Info($"断腕：{monster.Id} 当前意图不是攻击 → 击晕");
            await CreatureCmd.Stun(cardPlay.Target);
            AmbushEffects.Record(this);
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(3m);              // 6 → 9（2026-09-24 爆改成攻击牌时的定案）
            DynamicVars["WeakPower"].UpgradeValueBy(1m);         // 虚弱 1 → 2
        }
    }
}