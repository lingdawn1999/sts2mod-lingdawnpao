using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 集群伏击 Power：挂在雪豹自己身上，表示"伏击区的基础增益要分发给队友"。
    /// Counter 型挂 1 层即可，Amount 无实际含义。
    /// <para>
    /// **2026-09-25 用户重做（本次）**：改成**回合开始时给队友发持续一回合的临时增益**。
    /// 起因（原话）："防止雪豹变成增益架子，塞满力量等队友打完才配行动"——
    /// 旧实现是把区域力敏**持续镜像**给队友（区域涨一层，队友永久多一层，同增同减），
    /// 于是最优解退化成"雪豹自己什么都不干，只顾往区域里塞牌"，区域成了队友的永久增益架。
    /// 现在只在**雪豹回合开始那一刻**读一次区域张数，发放临时增益，队友**自己回合结束即收回**
    /// （见 <see cref="SwarmAmbushGrantPower" />）：区域存量依然是燃料，但队友拿到的只是"这一轮的量"。
    /// </para>
    /// <para>口径：只读**基础张数**（攻击牌数 → 力量、技能牌数 → 敏捷），
    /// **刻意不含「蓄势」的加值**——用户 2026-09-25 澄清"基础增益不含蓄势加成"。</para>
    /// <para>联机约束：本能力只在联机里存在（卡是 `MultiplayerOnly`），单人局挂不上。</para>
    /// </summary>
    public sealed class SwarmAmbushPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>
        /// 自己的回合开始时发一轮（照 <see cref="NightPatrolPower" /> 的写法：`participants` 过滤掉敌方回合，
        /// 无 choiceContext 时用 `ThrowingPlayerChoiceContext`）。
        /// </summary>
        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(base.Owner))
            {
                return;
            }

            Player? owner = base.Owner.Player;
            if (owner == null)
            {
                return;
            }

            await GrantToAlliesAsync(new ThrowingPlayerChoiceContext(), owner);
        }

        /// <summary>
        /// 按区域**当前基础张数**（攻击牌数 → 力量、技能牌数 → 敏捷）给所有存活队友发一份
        /// **持续到其回合结束**的临时增益。重复发放前先收回上一份（队友还没轮到自己的回合就又被打了一轮时，
        /// 不清旧账会叠成两份）。区域空 / 冷却中时只收回、不发放。
        /// </summary>
        public static async Task GrantToAlliesAsync(PlayerChoiceContext choiceContext, Player player)
        {
            Creature self = player.Creature;
            ICombatState? state = self.CombatState;
            if (state == null)
            {
                return;
            }

            AmbushZoneState zone = AmbushZone.For(player);
            // 只读基础张数：不含「蓄势」的 BonusExtra（用户 2026-09-25 澄清）
            AmbushZone.CountZoneTypes(zone, out int attacks, out int skills);
            int str = zone.IsCoolingDown ? 0 : attacks;
            int dex = zone.IsCoolingDown ? 0 : skills;

            foreach (Player ally in state.Players)
            {
                Creature creature = ally.Creature;
                if (creature == self || !creature.IsAlive)
                {
                    continue;
                }

                if (creature.GetPower<SwarmAmbushGrantPower>() is { } old)
                {
                    await old.RevokeAsync(choiceContext);
                }

                if (str == 0 && dex == 0)
                {
                    continue;
                }

                if (str != 0)
                {
                    await PowerCmd.Apply<StrengthPower>(choiceContext, creature, str, self, null);
                }

                if (dex != 0)
                {
                    await PowerCmd.Apply<DexterityPower>(choiceContext, creature, dex, self, null);
                }

                SwarmAmbushGrantPower? grant = await PowerCmd.Apply<SwarmAmbushGrantPower>(
                    choiceContext, creature, 1m, self, null);
                if (grant != null)
                {
                    grant.GrantedStr = str;
                    grant.GrantedDex = dex;
                }

                Log.Info($"集群伏击：给予队友 {ally.NetId} 本回合临时 {str} 力量 / {dex} 敏捷");
            }
        }
    }
}
