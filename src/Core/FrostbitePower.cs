using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 冰冻：Debuff（历史上只挂敌人，2026-09-20 起「冰封王座」也会把它挂到玩家自己身上）。
    /// 挂冰（施加或叠加）时立即结算一次等同当前层数的伤害——**对敌无视格挡、对己可格挡**
    /// （2026-09-24 用户定稿的"双标"，见 <see cref="ApplyFrostAsync" /> 里的 flag 说明），
    /// 然后**层数减半（向上取整）**，详见 <see cref="LayersRetainedDivisor" /> 的沿革说明。
    /// 灾厄式处决：任何时候生命值不高于冰冻层数（`<=`）即立即死亡（复用本体 DoomPower.DoomKill，
    /// 带灾厄特效与 AfterDiedToDoom 钩子）。挂冰结算逻辑集中在静态 ApplyFrostAsync，
    /// 所有施加冰冻的来源统一走该方法，保证"伤害→扣层→处决判定"顺序一致。
    /// </summary>
    public sealed class FrostbitePower : PowerModel
    {
        /// <summary>
        /// 每次触发结算后**保留下来的层数** = 当前层数 ÷ 此除数（**向上取整**）。
        /// **沿革**：2026-09-20 起是固定 -2 层（用户当时要求由 -1 削弱为 -2）。
        /// 但固定扣减永远扣不干净：伤害打的是"残留 + 本次"的**总层数**，
        /// 于是连打同一张挂 3 层的牌时伤害逐张爬升、残留一路上涨——
        /// 实测 5 张连打 = 3 / 4 / 5 / 6 / 7，合计 25 点还留 5 层，处决线同样无限抬高。
        /// **2026-09-22 用户判定整条冰冻线因此过强**，改为**减半**：层数按比例衰减，
        /// 数学上天然收敛（挂 N 层时单张伤害稳定在约 2N），残留不再无限累积、处决线也被夹住。
        /// **同日用户再定：减半按"向上取整"**（原实现是向下取整）——
        /// 也就是 1 层 → 留 1、3 层 → 留 2、5 层 → 留 3：**1 层冰冻不再自行消失**，
        /// 收敛点比向下取整更高一档（挂 3 层连打：3 / 5 / 6 / 6 …）。
        /// </summary>
        private const int LayersRetainedDivisor = 2;

        /// <summary>
        /// 批量挂冰（<see cref="ApplyFrostToAllAsync" />）的嵌套深度。
        /// **&gt;0 时抑制「雪隐」的挂冰回调**，改由批量方法在整批结束后统一回调一次。
        /// 目的：AOE 挂冰是"逐敌调用"，不抑制的话「雪隐」会**按敌人数**给格挡
        /// （3 敌 × 2 点 = 6 点），而本体同类设计 `Afterimage` 是"按牌数、不按目标数"。
        /// 用户 2026-09-21 定案："改成每次结算只给一次"。
        /// </summary>
        private static int _batchDepth;

        public override PowerType Type => PowerType.Debuff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>
        /// 挂冰入口：堆叠层数，立刻按当前层数结算一次直伤（**对敌无视格挡**），每段结算后层数减半。
        /// 所有施加冰冻的来源统一走该方法。玩家每有 1 层「冰封」能力，额外多触发一段。
        /// </summary>
        public static async Task<FrostbitePower?> ApplyFrostAsync(PlayerChoiceContext choiceContext, Creature target, decimal stacks, Creature? applier, CardModel? cardSource)
        {
            // 「凝寒」：施加者每层让**本次施加的层数**额外 +1（2026-09-21 新卡）。
            // 放在 Apply 之前，所以它同时抬高"这一次的挂冰伤害"与"残留层数"。
            // 只认 applier：冰封王座把冰冻挂到玩家自己身上时 applier 为空，不吃加成（避免自伤被自己的凝寒放大）。
            if (applier != null)
            {
                stacks += applier.GetPower<FrostIntensifyPower>()?.Amount ?? 0m;

                // 「狂欢时刻」（2026-09-23 用户重做）：**只有雪球 / 大雪球**的挂冰额外 +层数。
                // 放在这里而不是雪球的 OnPlay 里，是为了让「雪球发射器」「打雪仗」「雪场」这些
                // **自动打出雪球**的来源一并吃到（它们最终都是"打出雪球牌"，cardSource 就是那张雪球）。
                if (cardSource is Snowball or BigSnowball)
                {
                    stacks += applier.GetPower<CarnivalMomentPower>()?.Amount ?? 0m;
                }
            }

            FrostbitePower? power = await PowerCmd.Apply<FrostbitePower>(choiceContext, target, stacks, applier, cardSource);
            if (power == null || !target.IsAlive)
            {
                return power;
            }

            // 「融冬」：把触发结算整体改写成"不造成伤害 + 层数不减"（2026-09-22 用户改成纯开关，去掉了成长项）。
            // 判定放在循环外，避免每段重复查能力。
            bool meltForm = applier?.GetPower<WinterMeltPower>() != null;

            int triggers = 1 + (applier?.GetPower<FrozenAdeptPower>()?.ExtraTriggers ?? 0);
            for (int i = 0; i < triggers; i++)
            {
                if (!target.IsAlive)
                {
                    break;
                }

                // 融冬形态：既不结算伤害、也不衰减层数——层数只增不减
                // （因此不存在"层数被扣光后打负伤害"的问题，也不需要判 `Amount <= 0`）
                if (meltForm)
                {
                    continue;
                }

                // 层数已被扣光就不再触发：否则下一段会打出负伤害（等于给敌人回血）
                if (power.Amount <= 0)
                {
                    break;
                }

                // **2026-09-24 用户定稿的"双标"**：挂给**敌人**时**无视格挡**（`Unblockable`，
                // 本体口径 = "像中毒那样的生命损失"），挂给**我方自己**时仍走格挡。
                // 起因：用户判定"冰冻体系太弱、前期非常牢"——挂冰的主力全是技能牌，
                // 费用换来的即时伤害又常被敌人的格挡吃掉，整条线在前期几乎没有压制力。
                // 对敌无视格挡后，挂冰牌才真正等于"伤害牌"（这也是本 mod 把挂冰计入伤害职能的前提，
                // 见卡池注释里"对标毒猎人"的那段口径）。
                // **对己保持可格挡**是有意的不对称：自伤来自「冰封王座」「冰碎瓦全」「冰雪之躯」这类
                // "贷款换收益"的价码，玩家的既有格挡能垫掉它，才不至于把代价变成硬扣血。
                // 实现直接叠 flag（照「狩猎形态」的 `CreatureCmd.Damage` 前缀，
                // 见 Patches.cs 里给攻击伤害叠 Unblockable 的那处），下游 `DamageBlockInternal` 认这个 flag。
                //
                // 仍保留 Unpowered（不吃力量等伤害加成）——同本体「主宰」Juggernaut 的"事件触发型伤害"口径
                // （这张冰冻伤害原先是照「猎杀本能」写的，但那张金卡 2026-09-23 已重做成"每回合首次
                // 给费/摸牌"的运转件，不再造成伤害；口径改挂到本体的 Juggernaut 上）。
                // 注：这里曾经乘过「极寒」的伤害倍率，用户 2026-09-21 判定"体系后续会高频触发、倍率太强"而砍掉；
                // 若将来要加回，倍率就乘在这一行（本体没有冰冻伤害的钩子，只能在此手算）。
                ValueProp props = ValueProp.Unpowered;
                if (target.IsEnemy)
                {
                    props |= ValueProp.Unblockable;
                }

                await CreatureCmdCompat.Damage(choiceContext, target, (int)power.Amount, props,
                    applier, cardSource, null);
                // 每段把层数**减半、向上取整**：1 层 → 留 1（不再衰减）、3 层 → 留 2、5 层 → 留 3。
                // 上面已确保 Amount > 0，所以 retained ≥ 1、扣除量 ≤ 当前层数，不会减成负数。
                int current = (int)power.Amount;
                int retained = (current + LayersRetainedDivisor - 1) / LayersRetainedDivisor;
                int delta = retained - current;
                if (delta != 0)
                {
                    await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), power, (decimal)delta, applier, cardSource);
                }
            }

            // 挂冰回调：**按"每张牌施加一次"计**（用户 2026-09-20 定案），不吃「冰封」的多段触发。
            // 「雪隐」给格挡。批量模式下由 ApplyFrostToAllAsync 统一回调，这里跳过。
            // applier 为 null 时不触发——「冰封王座」把冰冻挂到玩家自己身上走的正是这条路，
            // 避免了"自伤反过来给自己送防"。
            if (applier != null && _batchDepth == 0)
            {
                await IceArmorPower.NotifyFrostAppliedAsync(choiceContext, applier);
            }

            return power;
        }

        /// <summary>
        /// **批量挂冰**：对多个目标依次走 <see cref="ApplyFrostAsync" />（每只敌人的挂冰伤害各自独立结算），
        /// 但「雪隐」的挂冰回调**只在整批结束后触发一次**——即"每次结算只给一次"，不随目标数放大
        /// （照本体 `Afterimage`"按牌数、不按目标数"的口径）。
        /// **所有"一次结算里给多个目标挂冰"的地方都要走这里**：霜原 / 霜天 / 暴风雪 / 寒灾 /
        /// 雪隐自身第 ② 条——直接循环调 <see cref="ApplyFrostAsync" />
        /// 会退回"按敌人数给格挡"。
        /// 内部用 <c>_batchDepth</c> 做作用域计数（支持嵌套），并保证异常时也能恢复。
        /// </summary>
        public static async Task ApplyFrostToAllAsync(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, decimal stacks, Creature? applier, CardModel? cardSource)
        {
            bool anyApplied = false;

            _batchDepth++;
            try
            {
                foreach (Creature target in targets)
                {
                    if (!target.IsAlive)
                    {
                        continue;
                    }

                    await ApplyFrostAsync(choiceContext, target, stacks, applier, cardSource);
                    anyApplied = true;
                }
            }
            finally
            {
                _batchDepth--;
            }

            // 整批只回调一次；且**确实挂过冰**才回调，避免空场也白送格挡
            if (applier != null && anyApplied)
            {
                await IceArmorPower.NotifyFrostAppliedAsync(choiceContext, applier);
            }
        }

        // 灾厄式处决：任何伤害结算后（含挂冰直伤、攻击伤害）检查 生命值 **不大于** 冰冻层数 即立即死亡。
        // 死亡本身不产生伤害，无重入风险；目标已被该次伤害击杀时本钩子不会被调用。
        // **2026-09-25 用户定案：`<` 改为 `<=`**（原话"层数刚刚好一样不斩杀也太扯了"）——
        // 生命值 == 层数时同样斩杀（照本体 DoomPower.IsOwnerDoomed 的 `CurrentHp <= Amount` 口径）。
        public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
        {
            if (target != base.Owner || base.Owner.IsDead || CombatManager.Instance.IsOverOrEnding || base.Owner.CurrentHp > base.Amount)
            {
                return;
            }

            Flash();
            await DoomPower.DoomKill(new Creature[] { base.Owner });
        }
    }
}
