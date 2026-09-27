using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 破绽（敌方 Debuff，由「狩猎形态」每回合施加）：受到的**所有**伤害增加 34%，持续 1 回合，
    /// **获得时随机移除该敌人 1 项增益**（2026-09-24 加，见下）。
    /// （2026-09-21 由"仅攻击伤害"扩大到所有伤害——用户："让所有伤害都能吃到破绽的增伤（包括冰冻的触发伤害）"）。
    /// 实现上只需拿掉 `props.IsPoweredAttack()` 这道门：冰冻触发伤害走的 `CreatureCmd.Damage`
    /// 本身就会跑 `Hook.ModifyDamage` 全链（本体 CreatureCmd.cs 的 Hook.ModifyDamage 调用点），
    /// 所以攻击、冰冻触发等一切伤害都会经过本钩子，无需改 FrostbitePower。
    /// 多层不叠乘：与本体 VulnerablePower 同款，返回固定倍率（破绽常态也只有 1 层）。
    /// 结构完全照本体 VulnerablePower：Counter 层数 + 敌方回合结束时 TickDownDuration；
    /// 走 PowerCmd.Apply 施加，所以会被人工制品挡掉一层（这正是用户要的效果——先破人工制品）。
    /// <para>
    /// **2026-09-24 用户定案：把"每回合清除敌方全体随机一项增益"绑进本能力**——
    /// 「狩猎形态」只负责每回合给所有敌人挂 1 层破绽，削增益由破绽自己承载
    /// （卡面文案保持简洁，细节走状态 tooltip）。
    /// </para>
    /// <para>
    /// ⚠️ 触发口径（`AfterApplied` 的真实行为，改前务必看 <c>PowerCmd.Apply</c>）：
    /// 这个钩子**只在 `PowerCmd.Apply` 走到"新实例"且 `modifiedAmount != 0` 时回调**；
    /// 走叠加路径（`FindExistingInstanceForStacking` → `ModifyAmount`）**不会回调**。
    /// 破绽的节奏刚好每次都是新实例：玩家回合开始挂 1 层 → 敌方回合结束 `TickDownDuration` 扣到 0
    /// → `PowerCmd.Remove`（所以每回合都会重新触发一次削增益，场上有几只敌人就削几条）。
    /// 被**人工制品**挡掉时 `modifiedAmount == 0` → 不触发（人工制品会逐层延迟削增益）。
    /// </para>
    /// </summary>
    public sealed class CalamityMarkPower : PowerModel
    {
        private const decimal _damageIncrease = 1.34m;

        public override PowerType Type => PowerType.Debuff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>
        /// 伤害乘区（+34%）。**刻意不是 override**：`ModifyDamageMultiplicative` 的签名在 public-beta 末尾
        /// 多了一个 `CardPlay?`，而 override 必须逐字匹配签名，一份 DLL 没法同时匹配两个分支。
        /// 改由 Patches.cs 的 `AbstractModel_ModifyDamageMultiplicative_PowerMultiplierPatch` 挂在基类实现上
        /// 按类型分发；判定口径与取值时机和原来的 override 完全一致（仍在本体伤害钩子链的同一步）。
        /// </summary>
        internal decimal DamageMultiplier(Creature? target) => target == base.Owner ? _damageIncrease : 1m;

        /// <summary>
        /// 获得破绽时**随机移除自身 1 项增益**（2026-09-24 用户定案）。
        /// 过滤走 <see cref="PowerStripHelper" /> 的两道闸（排除 boss 转阶段 / 核心机制，见该文件的沙虫事故说明）。
        /// applier 为空（非玩家施加）时不触发。
        /// </summary>
        public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
        {
            Player? applierPlayer = applier?.Player;
            if (applierPlayer == null)
            {
                return;
            }

            PowerModel? removed = await PowerStripHelper.StripRandomAsync(base.Owner, PowerType.Buff, applierPlayer);
            if (removed != null)
            {
                Log.Info($"破绽：随机移除增益 {removed.GetType().Name}");
            }
        }

        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (side == CombatSide.Enemy)
            {
                await PowerCmd.TickDownDuration(this);
            }
        }
    }
}