using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 追猎（敌方 Debuff，由「追猎」卡赋予）：被追猎的敌人会成为**从伏击区域带出的牌的锁定目标**——
    /// 正常情况下带出的牌跟随"本回合打出的那张牌"的目标，挂了追猎后一律改打被追猎的敌人。
    /// 层数 = 持续回合数（同本体虚弱/易伤的约定），敌方回合结束时递减，所以 1 层 = 本轮内有效、升级的 2 层 = 两轮。
    /// 另外：被追猎的敌人挨到从伏击区域带出的牌的攻击时，伤害×1.5。
    /// 作用域同样用 AmbushZone.IsAmbushPlay 限定，所以随便打的牌不吃这个加成。
    /// 目标重定向发生在 AmbushZone.BringOutFirstAsync，由本类的 FindTrackedTarget 提供目标。
    /// </summary>
    public sealed class TrackedPower : PowerModel
    {
        private const decimal _ambushDamageMultiplier = 1.5m;

        public override PowerType Type => PowerType.Debuff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>
        /// 伤害乘区（从伏击区域带出的牌打它时×1.5）。**刻意不是 override**：签名在 public-beta 末尾
        /// 多了一个 `CardPlay?`，override 必须逐字匹配签名，一份 DLL 没法同时匹配两个分支
        /// → 改由 Patches.cs 的 `AbstractModel_ModifyDamageMultiplicative_PowerMultiplierPatch` 挂在基类实现上按类型分发。
        /// </summary>
        internal decimal DamageMultiplier(Creature? target, ValueProp props, CardModel? cardSource)
        {
            if (target != base.Owner || !props.IsPoweredAttack())
            {
                return 1m;
            }

            if (cardSource == null || !AmbushZone.IsAmbushPlay(cardSource))
            {
                return 1m;
            }

            return _ambushDamageMultiplier;
        }

        /// <summary>当前被追猎的敌人（按可命中敌人的固定顺序取第一个，保证各客户端结果一致）。</summary>
        public static Creature? FindTrackedTarget(Player player)
        {
            IReadOnlyList<Creature>? enemies = player.Creature.CombatState?.HittableEnemies;
            if (enemies == null)
            {
                return null;
            }

            foreach (Creature enemy in enemies)
            {
                if (enemy.GetPower<TrackedPower>() != null)
                {
                    return enemy;
                }
            }

            return null;
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
