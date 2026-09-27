using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 雪隐（玩家增益，由「雪隐」**金卡能力**赋予，**永久常驻**）：
    /// ① 每当你给予敌人[gold]冰冻[/gold]，获得 N 点[gold]格挡[/gold]（**基础 2、升级 3**）；
    /// ② **你受到攻击而没有失去生命值**时，给予所有敌人 N 层[gold]冰冻[/gold]（**基础 1、升级 2**，值由卡注入）。
    /// ② 形成"挡得住 → 反挂冰 → 挂冰又给格挡"的自循环，只有被攻击时才推进，
    /// 所以强度受"这回合被攻击几次"天然限制；层数不带乘区，不会失控。
    /// 触发判定用 `AfterDamageReceived` + `result.UnblockedDamage == 0`（完全挡住或 0 伤），
    /// **不用 `BeforeDamageReceived`**——那个在伤害结算前跑，拿不到"有没有掉血"。
    /// 冰冻伤害是 `Unpowered`（非攻击）且此处已用 `IsPoweredAttack()` 门控，不会自我递归。
    /// ① 由 `FrostbitePower.ApplyFrostAsync` 末尾回调——**按"每张牌施加一次"计**，
    /// 不吃「冰封」的多段触发（用户 2026-09-20 定案）。
    /// **常驻能力（无自解钩子）**：用户 2026-09-21 最终定案为"**能力牌 + 金卡**"，
    /// 所以这里**不要**再加 `AfterSideTurnEnd` 之类的移除逻辑（曾经加过、已删）。
    /// </summary>
    public sealed class IceArmorPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>
        /// ② 无伤挡下攻击时给**所有敌人**的冰冻层数（基础 1、升级 2）。
        /// 能力只能从 `PowerCmd.Apply` 收到**一个** amount（本能力里那个是 ① 的格挡值），
        /// 所以第二个数值由「雪隐」卡在施加后注入（见 `IceArmor.OnPlay`）。
        /// </summary>
        public decimal FrostbiteStacks { get; set; } = 1m;

        /// <summary>挂冰入口回调：给施加者格挡。施加者身上没有本能力时什么都不做。</summary>
        public static async Task NotifyFrostAppliedAsync(PlayerChoiceContext choiceContext, Creature applier)
        {
            IceArmorPower? armor = applier.GetPower<IceArmorPower>();
            if (armor == null)
            {
                return;
            }

            await CreatureCmd.GainBlock(applier, armor.Amount, ValueProp.Unpowered, null, fast: true);
        }

        public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
        {
            // 只认"自己受到攻击且一点生命值都没掉"（完全挡住或 0 伤）；冰冻伤害是 Unpowered，被 IsPoweredAttack 挡在外面
            if (target != base.Owner || !props.IsPoweredAttack() || result.UnblockedDamage > 0)
            {
                return;
            }

            if (base.Owner.Player == null || base.Owner.IsDead)
            {
                return;
            }

            Flash();
            // 逐个敌人挂冰；遍历前取快照（挂冰直伤可能打死敌人）——
            // 走**批量入口**：本批只回调一次「雪隐」的格挡（即自己这次不会按敌人数放大格挡）
            await FrostbitePower.ApplyFrostToAllAsync(choiceContext,
                base.Owner.CombatState!.HittableEnemies.ToList(), FrostbiteStacks, base.Owner, null);
        }
    }
}