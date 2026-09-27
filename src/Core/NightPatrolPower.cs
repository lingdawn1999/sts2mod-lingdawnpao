using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 暗夜收割（玩家增益，由「暗夜收割」金卡赋予）：**每回合一次的通用成长件**。
    /// 你的回合开始时，只要[gold]伏击区域[/gold]里**还有牌**，就永久获得 <see cref="PowerModel.Amount" /> 点
    /// [gold]力量[/gold]与同等[gold]敏捷[/gold]（同本体 `DemonFormPower`，收益从**下个回合**开始，
    /// 打出当回合不结算）。
    /// <para>
    /// **2026-09-23 用户定稿的大改（第三版，前两版全部废弃）**：前两版的收益触发都是"张数制"
    /// （① 埋入即累积 → ② 伏击区内的牌被消耗时逐张累积），结果都能被**免费燃料刷爆**：
    /// 雪球（`CardRarity.Token`，0 费、可批量生成、自带消耗）与晕眩（`CardRarity.Status`，雪灾自产）
    /// 批量喂进去就能每张白拿 2 力量。**根因不是"燃料太便宜"，而是触发本身可以在一回合内反复发生**。
    /// </para>
    /// <para>
    /// **改法：把硬闸设在"每回合一次"上，而不是设在"哪种牌才算数"上。** 现在触发只读
    /// "回合开始那一刻区域是否非空"——不管你堆了 1 张还是 5 张、是雪球还是真牌，一回合永远只结算一次。
    /// 于是：① 燃料彻底失去套利价值（0 费塞一张和塞五张完全一样）；② 不需要再引入
    /// "只算真牌/只算花过费用的牌"那类过滤，规则面保持干净；③ 收益对**所有体系**通用
    /// ——力量喂攻击与埋伏兑现，敏捷喂格挡（冰冻流也能靠它保命）。
    /// </para>
    /// <para>
    /// **条件为什么是"区域非空"而不是"区域已满"**：埋伏线的常态是"埋进 → 带出 → 再埋"，
    /// 带出会让区域变空，所以"非空"是玩家**每回合都够得着**的门槛（寒一张 0 费牌即可），
    /// 而"已满"会把「倾泻」类玩法直接判死。用户 2026-09-23 明确选前者。
    /// </para>
    /// <para>
    /// ⚠️ **不要把它改回"张数制"**（那正是被雪球/晕眩刷爆的形态），也不要按
    /// "被消耗/被带出"来触发——那些动作一回合能发生很多次，天然不可控。
    /// </para>
    /// </summary>
    public sealed class NightPatrolPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        /// <summary>
        /// Single：这是"有无"型成长引擎，**层数没有语义**（成长值固定读 <see cref="PowerModel.Amount" />，
        /// 由卡面一次性写入）——刻意不用 Counter，避免玩家误以为叠层能加速成长。
        /// </summary>
        public override PowerStackType StackType => PowerStackType.Single;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromPower<StrengthPower>(),
            HoverTipFactory.FromPower<DexterityPower>(),
        ];

        /// <summary>
        /// 自己回合开始时结算一次。`participants` 过滤照抄本体 `DemonFormPower`：
        /// 敌方回合也会触发本钩子，不过滤会把成长送给对手回合。
        /// </summary>
        public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(Owner))
            {
                return;
            }

            Player? player = Owner.Player;
            if (player == null)
            {
                return;
            }

            AmbushZoneState zone = AmbushZone.For(player);
            if (zone.Count == 0)
            {
                // 区域空 = 这一回合没"收割"到东西，不结算（也不惩罚，只是错过本回合）
                Log.Info("暗夜收割：回合开始时伏击区域为空，本回合不结算");
                return;
            }

            Flash();
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
            await PowerCmd.Apply<DexterityPower>(
                new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
            Log.Info($"暗夜收割：回合开始时伏击区域内有 {zone.Count} 张牌，永久获得 {Amount} 力量与 {Amount} 敏捷");
        }
    }
}