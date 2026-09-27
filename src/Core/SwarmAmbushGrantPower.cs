using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 「集群伏击」发给队友的**临时增益载体**（2026-09-25 用户重做）：记录本次授予的力量 / 敏捷，
    /// 在**队友自己的回合结束时**（`AfterSideTurnEnd`，`side == Owner.Side`）把等量负值加回去并自解。
    /// 于是闭环成立：雪豹回合开始发放 → 队友带着增益打完自己的回合 → 收回。
    /// <para>
    /// `StackType = Single`：它只是"记账标记"、不显示层数（照 <see cref="NightPatrolPower" />）。
    /// 收回用 `PowerCmd.Apply&lt;StrengthPower&gt;(..., -GrantedStr, ...)`——本体的
    /// `StrengthPower.AllowNegative` 支持负值（同「收网」的降力量写法）。
    /// </para>
    /// <para>
    /// ⚠️ **只在自己回合结束时收回**：队友若在收回前阵亡，增益会随尸体一起消失（`IsAlive` 已在发放侧过滤）。
    /// `RevokeAsync` 同时兼作"重复授予前清旧账"，见 <see cref="SwarmAmbushPower.GrantToAlliesAsync" />。
    /// </para>
    /// </summary>
    public sealed class SwarmAmbushGrantPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        /// <summary>本次授予的力量值（收回时按它取负）。</summary>
        public int GrantedStr { get; set; }

        /// <summary>本次授予的敏捷值（收回时按它取负）。</summary>
        public int GrantedDex { get; set; }

        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
        {
            if (side != base.Owner.Side || !participants.Contains(base.Owner))
            {
                return;
            }

            await RevokeAsync(choiceContext);
        }

        /// <summary>把授予的力敏原数收回并移除自己（重复授予前也调它清旧账）。</summary>
        public async Task RevokeAsync(PlayerChoiceContext choiceContext)
        {
            if (GrantedStr != 0)
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, base.Owner, -GrantedStr, base.Owner, null);
                GrantedStr = 0;
            }

            if (GrantedDex != 0)
            {
                await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner, -GrantedDex, base.Owner, null);
                GrantedDex = 0;
            }

            await PowerCmd.Remove(this);
        }
    }
}
