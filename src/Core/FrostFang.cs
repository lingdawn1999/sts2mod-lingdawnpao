using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 初始遗物「伏击者」（v2）：伏击体系的引擎挂载点。四条款（设计稿规则 8）：
    /// ① 每回合你的第一张【本来要进弃牌堆】的牌，结算后改道加入伏击区域
    ///    （<see cref="AfterCardPlayed" /> → <see cref="AmbushZone.TryDivertAfterPlayAsync" />）；
    /// ② 区域中攻击牌 +1 力量、技能牌 +1 敏捷（<see cref="AmbushZone.RecomputeBonusesAsync" />）；
    /// ③ 每回合首次从区域带出牌后，抽 1 张牌（<see cref="NotifyFirstCashOutAsync" />）；
    /// ④ 回合结束若区域已满：上限 +1 并由玩家自选烧一张牌（<see cref="AfterSideTurnEnd" />）。
    /// <para>
    /// 联机：改道发生在遗物 AfterCardPlayed 钩子里（本体出牌动作内部，本来就全端执行），
    /// 不引入新的同步动作类型；烧牌成长与回合钩子同样全端跑同一份逻辑。
    /// </para>
    /// </summary>
    public class FrostFang : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Starter;

        public override async Task BeforeCombatStart()
        {
            Player? owner = Owner;
            AmbushZoneState? zone = owner == null ? null : AmbushZone.For(owner);
            // **进战斗的兜底重置**：冷却/过载是"本回合"级别的状态，正常由 ReturnBuriedCardsAsync
            // （AfterCombatEnd）清零。但战斗结束流程并不保证一定跑到那里——本体结算中途抛异常、
            // 玩家放弃对局等都会漏掉；额度与兑现记账同理（漏掉的表现：第一回合少/多改道一张、
            // 首兑现抽牌被上一场的回合号误伤）。负责解除冷却的 ConcentrationPower / OverloadPower
            // 都是能力（战斗结束即被清掉）→ 没人能解开，必须在这里兜底。
            if (zone != null)
            {
                if (zone.IsCoolingDown || zone.IsOverloaded)
                {
                    Log.Info($"伏击者：进战斗时发现残留状态（冷却={zone.IsCoolingDown}，过载={zone.IsOverloaded}），已清除");
                    zone.IsCoolingDown = false;
                    zone.IsOverloaded = false;
                }

                zone.QuotaUsed = 0;
                // 「十万埋伏」的伤害成长按"本场战斗"计，进战斗必须归零（重复进战斗时同理）
                zone.AmbushDamageCount = 0;
                // 【v2】首次兑现抽牌的记账回合号也要跨战斗重置——
                // TurnNumber 每场战斗从 1 重新计数，上一场战斗的"已结算回合号"会误伤本场相同回合号
                _firstCashOutTurn = -1;
            }

            // 挂上伏击区上限能力（开局就有 buff 提示伏击区上限）：
            // 基础 MaxSize 层，暗杀者（先古遗物）额外 +1。差分应用——重战中复挂只补差额，
            // 不会叠出 MaxSize*2。该能力战斗结束随 PowerModel 默认清理流程消失。
            if (owner != null)
            {
                int baseCap = AmbushZone.MaxSize + (this is Assassin ? Assassin.AmbushCapacityBonus : 0);
                var creature = owner.Creature;
                int current = (int)(creature.GetPower<AmbushCapacityPower>()?.Amount ?? 0);
                if (current != baseCap)
                {
                    await PowerCmd.Apply<AmbushCapacityPower>(
                        new ThrowingPlayerChoiceContext(), creature, baseCap - current, creature, null);
                    Log.Info($"伏击者：伏击区上限能力挂上 {baseCap} 层");
                }
            }

            Log.Info($"伏击者：进入战斗（区域牌数={zone?.Count}，额度余={zone?.QuotaLeft}）");
        }

        /// <summary>
        /// 【v2 核心】改道挂点：打出的牌**结算完毕**后，若本回合额度未用且区域未满，
        /// 把它从打牌堆挪进伏击区域——双分支确认 CardModel.PlayAsync 在 AfterCardPlayed 之后
        /// 才检查 <c>Pile == Play</c> 决定去向，把卡挪走后原版收尾自动跳过，无需新 Harmony 补丁。
        /// <para>
        /// 五条件（非自动打出 / 本人卡 / 非能力·消耗·复制 / 有额度 / 区域未满）
        /// 统一收口在 <see cref="AmbushZone.TryDivertAfterPlayAsync" />，这里只做 owner 过滤：
        /// 联机时本钩子会收到所有玩家的出牌（本体 28 个用它的遗物里 26 个都做了 owner 过滤），
        /// 不过滤的话队友出牌会消耗我们自己的入区额度。
        /// </para>
        /// </summary>
        public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            Player? owner = Owner;
            if (owner == null || cardPlay.Card.Owner != owner)
            {
                return;
            }

            await AmbushZone.TryDivertAfterPlayAsync(choiceContext, owner, cardPlay);
        }

        /// <summary>
        /// 【v2】回合开始：重置本回合的入区额度（基础 1 张 + 永恒狩猎层数）。
        /// 只在**自己一方**的回合开始时重置——敌方回合开始不清我们的额度。
        /// </summary>
        public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
            IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            Player? owner = Owner;
            if (owner == null || !participants.Contains(owner.Creature))
            {
                return Task.CompletedTask;
            }

            AmbushZone.For(owner).QuotaUsed = 0;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 【v2】回合结束：满仓成长——区域已满则弹选牌，烧一张（自选，token/诅咒/状态牌皆可）
        /// 换上限 +1。详细规则见 <see cref="AmbushZone.AfterTurnEndGrowthAsync" />。
        /// </summary>
        public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
            IEnumerable<Creature> participants)
        {
            Player? owner = Owner;
            if (owner == null || !participants.Contains(owner.Creature))
            {
                return;
            }

            await AmbushZone.AfterTurnEndGrowthAsync(choiceContext, owner,
                new LocString("relics", "FROST_FANG.burnPrompt"));
        }

        /// <summary>
        /// 【v2】每回合**首次**兑现带出牌后抽 1 张牌（遗物新条款，2026-09-26）。
        /// 由 <see cref="AmbushZone.BringOutFirstAsync" />（带出唯一出口）调用；
        /// 照 KillerInstinct 的 TurnNumber 记账法——钩子漏跑只会过期一回合，不会永久卡死。
        /// </summary>
        private int _firstCashOutTurn = -1;

        public async Task NotifyFirstCashOutAsync(PlayerChoiceContext choiceContext, Player player)
        {
            int? turn = player.PlayerCombatState?.TurnNumber;
            if (turn == null || _firstCashOutTurn == turn.Value)
            {
                return;
            }

            _firstCashOutTurn = turn.Value;
            Flash();
            await CardPileCmd.Draw(choiceContext, 1, player);
            Log.Info($"伏击者：本回合首次兑现，抽 1 张牌（回合 {turn.Value}）");
        }

        /// <summary>
        /// 「十万埋伏」的成长记账：**本场战斗中，你的伏击牌每造成一次伤害，计数 +1**
        /// （记在 <see cref="AmbushZoneState.AmbushDamageCount" /> 上，卡打出时按它换算伤害）。
        /// <para>
        /// **口径**（对应 2026-09-24 用户定的"本场战斗你每因伏击造成过一次伤害"）：
        /// ① 出伤者必须是本人（`dealer` 过滤，否则队友的伏击牌会给你的计数灌水）；
        /// ② 伤害必须来自**从伏击区域带出打出的牌**（<see cref="AmbushZone.IsAmbushPlay" />）；
        /// ③ 必须是**攻击伤害**（`IsPoweredAttack`）且 `TotalDamage > 0`——漏掉这条的话，
        ///    带出牌的挂冰伤害、其它能力的衍生伤害都会重复计数；
        /// ④ 一次伤害结算记一次，所以多段攻击 / 一次带出多张会明显加速成长，这正是这张金卡的正反馈来源。
        /// </para>
        /// <para>
        /// ⚠️ 纯记账钩子，**不产生任何战斗动作**，且各客户端在同步的动作序列里跑同一份逻辑，
        /// 计数天然一致（所以卡面用动态伤害显示也不会两端分叉）。改口径时务必保持"只依赖已同步的信息"。
        /// </para>
        /// </summary>
        public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
            ValueProp props, Creature target, CardModel? cardSource)
        {
            Player? owner = Owner;
            if (owner == null
                || dealer != owner.Creature
                || cardSource == null
                || !AmbushZone.IsAmbushPlay(cardSource)
                || !props.IsPoweredAttack()
                || result.TotalDamage <= 0)
            {
                return Task.CompletedTask;
            }

            AmbushZone.For(owner).AmbushDamageCount++;
            return Task.CompletedTask;
        }

        // 战斗结束：未带出的埋牌归入弃牌堆，区域清空
        public override async Task AfterCombatEnd(CombatRoom room)
        {
            Player? owner = Owner;
            if (owner == null)
            {
                return;
            }

            AmbushZoneState zone = AmbushZone.For(owner);
            zone.AmbushDamageCount = 0;   // 「十万埋伏」的成长是"本场战斗"口径，战后清账
            await AmbushZone.ReturnBuriedCardsAsync(owner);
            Log.Info($"伏击者：战斗结束，区域已清空");
        }
    }
}
