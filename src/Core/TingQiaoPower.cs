using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 听桥（玩家增益，由「听桥」卡赋予，无层数语义 → `Single`）：
    /// ① **受到的伤害减少 60%**（即乘 0.4）；② **敌人挥出攻击时，先反手打它一下**
    /// —— 反弹量 = **该次攻击的单段面板伤害**（`AttackCommand` 的原始 `_damagePerHit`，不含任何加成），
    /// 用 `ValueProp.Move` + `dealer = 自己` 打出，于是**自动吃玩家自己的力量**
    /// （`StrengthPower.ModifyDamageAdditive` 命中 `base.Owner == dealer && props.IsPoweredAttack()`）。
    /// 持续到**你的下个回合开始**（跨过敌方回合）。
    ///
    /// **2026-09-22 用户改口径（原为 `BeforeDamageReceived` 的"受伤后反弹减伤前的值"）**：
    /// 原话"听桥的时机要改成敌人攻击结算开始前，比如我打出了听桥，敌人当前有 15 血，造成 16 伤害，
    /// 那么他攻击时我会在他攻击同时对他造成 16 伤害，如果他因此死亡，那么他造成的伤害就不再结算了，
    /// 如果没死，就正常受伤并且减伤"，随后补充"我指的听桥吃力量是指吃玩家自己的力量，反弹伤害直接
    /// 按照怪物的意图伤害+玩家力量来算伤害就行了。然后如果怪物是多段攻击，那就只反弹一次一段面板伤害，
    /// 比如 3×2，那就反弹 3+力量。"
    ///
    /// **为什么必须挂在 `BeforeAttack`（而不是受伤时）**：
    /// [AttackCommand.Execute](file:///d:/sts2mod/sts2mod-unciphered/src/Core/Commands/Builders/AttackCommand.cs#L513-L658)
    /// 的顺序是 `await Hook.BeforeAttack(...)`（第 536 行）→ 进循环，而**循环每段开头都有
    /// `if (Attacker.IsDead) break;`（第 540~543 行）**。所以在 `BeforeAttack` 里把攻击者打死，
    /// 原攻击的每一段都会直接 break 掉 = "他造成的伤害不再结算"；没打死则循环照常跑，
    /// 玩家照常受伤（并被本能力的 0.4 减伤）。若改成受伤时反弹，伤害已经落下来了，无法撤销。
    ///
    /// **多段只反弹一次、且只按单段面板算**：`BeforeAttack` 对**一次攻击**只回调一次（多段共用同一个
    /// command），天然满足"只反弹一次"；面板值取 `_damagePerHit` 也就是单段值（3×2 反弹 3 + 力量，不是 6）。
    ///
    /// **`_damagePerHit` 是 private readonly**，故用 Harmony 的 `FieldRefAccess` 读；
    /// `AttackCommand` 只给钩子暴露了 Attacker / TargetSide / DamageProps / IsSingleTargeted 等，
    /// **没有**任何"每段伤害"的公开访问器（`Results` 要 Execute 完才有）。怪物攻击走的是
    /// `_damagePerHit` 这一支（`_calculatedDamageVar` 是卡牌专用，那时该字段为 -1）→ 读不到正值就跳过。
    ///
    /// **目标判定用 `command.TargetSide == CombatSide.Player`**：`AttackCommand` 的 `_singleTarget` 同样是
    /// private，钩子里拿不到具体目标；单机下"打向玩家方"即"打自己"，精度足够。多人下若队友被打，
    /// 自己身上的听桥也会误触发一次——属已知取舍（本 mod 目前未做多人适配）。
    ///
    /// **不会无限递归**：反弹的 target 是敌人，本能力的钩子只认"攻击者是敌人"；
    /// 就算敌人带本体荆棘（它用 `Unpowered` 反弹），`IsPoweredAttack()` 为 false，链条到此为止。
    /// </summary>
    public sealed class TingQiaoPower : PowerModel
    {
        /// <summary>减伤后剩余的倍率：0.4 = 减伤 60%。</summary>
        private const decimal DamageMultiplierAfterReduction = 0.4m;

        /// <summary>
        /// `AttackCommand._damagePerHit`（该攻击的**单段面板伤害**，未含任何加成）。
        /// 用 `FieldInfo` + 空判而不是 `FieldRefAccess`：本体若改字段名，这里只会读不到值而**静默跳过**
        /// （听桥退化成"只减伤不反弹"），不会在战斗中途抛异常把局面卡死。调用频率极低（每次敌人攻击一次），
        /// 装箱开销可忽略。
        /// </summary>
        private static readonly System.Reflection.FieldInfo? DamagePerHitField =
            AccessTools.Field(typeof(AttackCommand), "_damagePerHit");

        /// <summary>
        /// `AttackCommand._singleTarget`（单体攻击的目标；群体攻击时为 null）。
        /// 多人适配：单体攻击只让**被打的那个玩家**的听桥反弹，避免队友被打时我也跟着反弹
        /// （群体攻击打所有玩家，每个有听桥的玩家都反弹，属合理行为）。
        /// 同 `_damagePerHit` 的容错策略：读不到值时退化为旧行为（TargetSide == Player 即触发）。
        /// </summary>
        private static readonly System.Reflection.FieldInfo? SingleTargetField =
            AccessTools.Field(typeof(AttackCommand), "_singleTarget");

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        /// <summary>
        /// 受到的伤害乘 0.4（减伤 60%）。照本体易伤/破绽同款钩子。
        /// **刻意不是 override**：签名在 public-beta 多了一个 `CardPlay?`，override 必须逐字匹配签名，
        /// 一份 DLL 没法同时匹配两个分支 → 改由 Patches.cs 的
        /// `AbstractModel_ModifyDamageMultiplicative_PowerMultiplierPatch` 挂在基类实现上按类型分发。
        /// </summary>
        internal decimal DamageMultiplier(Creature? target) =>
            target == base.Owner ? DamageMultiplierAfterReduction : 1m;

        /// <summary>
        /// 敌人挥出攻击、**伤害还未结算**时先反手打它一下（见类注释的时序依据）。
        /// 多人适配：单体攻击只在**自己被打**时反弹（读 `_singleTarget`）；群体攻击打所有玩家，
        /// 每个有听桥的玩家都反弹，属合理行为。
        /// </summary>
        public override async Task BeforeAttack(AttackCommand command)
        {
            if (command.Attacker is not { } attacker || attacker.Side != CombatSide.Enemy)
            {
                return;
            }

            if (command.TargetSide != CombatSide.Player || base.Owner.IsDead || attacker.IsDead)
            {
                return;
            }

            // 多人适配：单体攻击只让被打的玩家反弹；群体攻击（_singleTarget == null）所有玩家都反弹
            Creature? singleTarget = SingleTargetField?.GetValue(command) as Creature;
            if (singleTarget != null && singleTarget != base.Owner)
            {
                return;
            }

            decimal panel = DamagePerHitField?.GetValue(command) is decimal value ? value : 0m;
            if (panel <= 0m)
            {
                return;
            }

            Flash();
            // ValueProp.Move + dealer 传自己：让反弹吃玩家自己的力量（Unpowered 会被 StrengthPower 跳过）。
            // 用两分支都有的 5 参重载（等价于 cardSource 传 null，见 BranchCompat 的说明）。
            await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), attacker, panel,
                ValueProp.Move, base.Owner);
        }

        /// <summary>敌方回合结束时移除 = 效果持续到你的下个回合开始（照 FlameBarrierPower）。</summary>
        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (base.Owner.Side != side)
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}