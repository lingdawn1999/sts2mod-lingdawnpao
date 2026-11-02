using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 「永恒狩猎」能力（v1）：每回合你的**免费埋牌额度** + 等同层数张，且免费埋牌不再受「费用不高于1」的限制。
    /// <para>
    /// 结算模型（双源核对：v0.1.7 官方 DLL 反编译 + 快照版 AmbushZone 的 NotifyBury 调用点）：
    /// ① <see cref="NotifyBury" />：每次埋入时由 <see cref="AmbushZone" /> 调用，给本回合已埋计数 +1；
    /// ② <see cref="TryModifyEnergyCostInCombatLate" />：打出**会进伏击区域**的牌时把费用清零——
    ///    但要过 <see cref="ShouldSkip" /> 五道闸：本人卡 / 牌在手牌或打出中（Hand/Play，
    ///    Deck 里的卡牌组阶段不算）/ 本回合免费额度没用完 / 遗物伏击形态开启 / 区域未满且未冷却。
    /// 「费用不高于1」的基础限制与基础额度在本体免费埋牌管线（v1 FrostFang / ShouldBury 一侧），
    /// 本能力只负责加层数和解限制的差额部分。
    /// </para>
    /// </summary>
    public sealed class EternalHuntPower : PowerModel
    {
        private int _buriedThisTurn;

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (!participants.Contains(Owner))
            {
                return Task.CompletedTask;
            }

            _buriedThisTurn = 0;
            if (Owner.Player is { } player) AmbushZone.SyncCardCosts(player);
            return Task.CompletedTask;
        }

        /// <summary>埋入时计数（由 <see cref="AmbushZone" /> 埋牌路径调用），耗完本回合免费额度。</summary>
        public void NotifyBury()
        {
            _buriedThisTurn++;
            if (Owner.Player is { } player) AmbushZone.SyncCardCosts(player);
            Log.Info($"永恒狩猎：本回合已埋入 {_buriedThisTurn} 张（免费额度 {Amount} 张）", 2);
        }

        public override Task AfterApplied(Creature? applier, CardModel? cardSource)
        {
            if (Owner.Player is { } player) AmbushZone.SyncCardCosts(player);
            return Task.CompletedTask;
        }

        public override Task AfterRemoved(Creature oldOwner)
        {
            if (oldOwner.Player is { } player) AmbushZone.SyncCardCosts(player);
            return Task.CompletedTask;
        }

        /// <summary>打出会进伏击区域的牌 → 费用清零（过五道闸，见 <see cref="ShouldSkip" />）。</summary>
        public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
        {
            modifiedCost = originalCost;
            if (ShouldSkip(card))
            {
                return false;
            }

            modifiedCost = default;
            return true;
        }

        private bool ShouldSkip(CardModel card)
        {
            // ① 不是本人打出的牌
            if (card.Owner.Creature != Owner)
            {
                return true;
            }

            // ② 牌必须在手牌（打出前）或打出中（Play 堆）——牌组阶段的牌不享受
            PileType? pileType = card.Pile?.Type;
            if (pileType is not (PileType.Hand or PileType.Play))
            {
                return true;
            }

            // ③ 本回合免费额度已用完
            if (_buriedThisTurn >= Amount)
            {
                return true;
            }

            Player? player = Owner.Player;
            if (player == null)
            {
                return true;
            }

            // ④⑤ 常规形态（未开伏击形态）/ 区域冷却 / 区域已满 → 不免费
            AmbushZoneState zone = AmbushZone.For(player);
            return !zone.IsAmbushMode || zone.IsCoolingDown || zone.IsFull;
        }
    }
}
