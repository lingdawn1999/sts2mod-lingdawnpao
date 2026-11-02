using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 埋牌动作：伏击形态下拦截出牌后不再就地改状态，而是把这个动作丢进 ActionQueueSynchronizer，
    /// 由它在每个客户端上按同一顺序执行（本体出牌走的就是这条路），联机因此不会脱同步。
    /// 本体 ActionTypes.Initialize 会把 mod 里的 INetAction 子类一并注册进网包类型表，
    /// 所以自定义动作可以正常序列化传输。
    /// </summary>
    public sealed class BuryCardAction : GameAction
    {
        public BuryCardAction(CardModel card)
        {
            Player = card.Owner;
            NetCard = NetCombatCard.FromModel(card);
        }

        public BuryCardAction(Player player, NetCombatCard card)
        {
            Player = player;
            NetCard = card;
        }

        public Player Player { get; }

        public NetCombatCard NetCard { get; }

        public override ulong OwnerId => Player.NetId;

        // 和出牌一致：只在出牌阶段执行，回合外请求会被推迟到自己回合开始
        public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

        protected override async Task ExecuteAction()
        {
            CardModel? card = NetCard.ToCardModelOrNull();
            if (card == null)
            {
                Log.Info("伏击者：埋牌动作找不到对应的牌（可能已不在战斗中），跳过");
                PendingBuryVisual.Resolve(Player, NetCard, buried: true); // 牌已不在局内，无从归还
                return;
            }

            CardPile? pile = card.Pile;
            AmbushZoneState zone = AmbushZone.For(Player);
            if (pile == null || pile.Type != PileType.Hand || zone.IsFull || zone.IsCoolingDown
                || Player.PlayerCombatState == null || Player.GetRelic<FrostFang>() == null
                || !card.CanPlay(out UnplayableReason _, out AbstractModel? _))
            {
                Log.Info($"伏击者：埋牌动作条件不再满足（牌堆={pile?.Type}，区域={zone.Count}/{zone.Capacity}，冷却={zone.IsCoolingDown}），跳过");
                // 关键：此时卡面 holder 还挂在 NPlayerHand 的等待队列里无人认领，
                // 必须归还手牌，否则就是"隐形但可打出的牌"（2026-09-28 用户报障）
                PendingBuryVisual.Resolve(Player, NetCard, buried: false);
                return;
            }

            Log.Info($"伏击者：执行埋牌 {card.Id.Entry}（玩家 {Player.NetId}）");
            try
            {
                await AmbushZone.BuryAsync(new GameActionPlayerChoiceContext(this), card, Player);
            }
            finally
            {
                PendingBuryVisual.Resolve(Player, NetCard, buried: card.Pile?.Type != PileType.Hand);
            }
        }

        public override INetAction ToNetAction() => new NetBuryCardAction { card = NetCard };

        public override string ToString() => $"BuryCardAction card: {NetCard.ToCardModelOrNull()} index: {NetCard.CombatCardIndex}";
    }

    /// <summary>
    /// 埋牌跳过时的**视觉回滚**（对"卡牌消失但卡位还在、还能打出"bug 的修复，2026-09-28）：
    /// <para>
    /// **病灶**：拦截出牌时 <c>NCardPlay.Cleanup(true)</c> 发出 Finished(success=true)，
    /// 手牌的 <see cref="NHandCardHolder" /> 被挂进 NPlayerHand 的 _holdersAwaitingQueue，
    /// 等"队列里的出牌动作"来收走（本体出牌就是这样交接的）。本 mod 队列里排的是
    /// <see cref="BuryCardAction" /> 而不是本体出牌动作——它执行成功时由
    /// <c>AmbushZone.BuryAsync</c> 的 AddDuringManualCardPlay 亲手收走 holder；
    /// 但若执行时条件不再满足（区域被塞满 / 进入冷却 / 牌变得不可打）而**跳过**，
    /// holder 就永远无人认领：卡面节点消失、模型还在手牌堆 → **隐形可打出的牌**。
    /// 区域只剩 1 格时快速连埋两张即可复现。
    /// </para>
    /// <para>
    /// **修法**：拦截时记住被挂起的 holder；埋牌动作执行失败时照本体 Finished(false) 的路径
    /// 反射调 <c>NPlayerHand.ReturnHolderToHand</c> 把卡面按模型插回原手牌槽位
    /// （模型从未离开过手牌堆，插回位置正确）。NCardPlay 只存在于发起拦截的本地端，
    /// 其余客户端 pending 为空，Resolve 是空操作——联机安全。
    /// </para>
    /// </summary>
    internal static class PendingBuryVisual
    {
        private static readonly PendingBuryEntries<NHandCardHolder> Entries = new();

        public static void Register(NCardPlay play)
        {
            if (play.Holder.CardModel is not { } card) return;
            Entries.Register(card.Owner.NetId, NetCombatCard.FromModel(card).CombatCardIndex, play.Holder);
        }

        public static void Clear(Player player) => Entries.Clear(player.NetId);

        public static void Resolve(Player player, NetCombatCard netCard, bool buried)
        {
            NHandCardHolder? holder = Entries.Take(player.NetId, netCard.CombatCardIndex);
            if (buried || holder == null || !Godot.GodotObject.IsInstanceValid(holder)) return;
            try
            {
                if (holder.CardModel?.Pile?.Type == PileType.Hand
                    && NCombatRoom.Instance?.Ui?.Hand is NPlayerHand hand)
                {
                    AccessTools.Method(typeof(NPlayerHand), "ReturnHolderToHand")?.Invoke(hand, new object?[] { holder });
                    if (holder.CardNode is { } node) AmbushCardVisuals.RestoreForHand(node);
                    holder.UpdateCard();
                    Log.Info("伏击者：埋牌未执行，已归还对应手牌卡面");
                }
            }
            catch (Exception e)
            {
                Log.Warn($"伏击者：归还埋牌卡面失败（{e.GetType().Name}: {e.Message}）");
            }
        }
    }

    public struct NetBuryCardAction : INetAction, IPacketSerializable
    {
        public NetCombatCard card;

        public GameAction ToGameAction(Player player) => new BuryCardAction(player, card);

        public void Serialize(PacketWriter writer) => writer.Write(card);

        public void Deserialize(PacketReader reader) => card = reader.Read<NetCombatCard>();

        public override string ToString() => $"NetBuryCardAction ({card})";
    }

    /// <summary>
    /// 切换伏击形态的动作。形态会被遗物 AfterCardPlayed 钩子（全端执行）读取来决定是否带出埋牌，
    /// 所以它也必须同步：右键只负责入队，真正翻转在所有客户端上一起发生。
    /// </summary>
    public sealed class ToggleAmbushAction : GameAction
    {
        public ToggleAmbushAction(Player player, bool enabled)
        {
            Player = player;
            Enabled = enabled;
        }

        public Player Player { get; }

        public bool Enabled { get; }

        public override ulong OwnerId => Player.NetId;

        // 战斗内外都能切（战斗外右键遗物预设形态），且不应在战斗结束时被取消
        public override GameActionType ActionType => GameActionType.Any;

        protected override Task ExecuteAction()
        {
            AmbushZone.For(Player).IsAmbushMode = Enabled;
            // Refresh after changing state, before any relic animation that could skip/interrupt its UI callback.
            AmbushZone.SyncCardCosts(Player);
            LingdawnpaoAnimationController.SyncMode(Player);
            Player.GetRelic<FrostFang>()?.OnAmbushModeSynced();
            return Task.CompletedTask;
        }

        public override INetAction ToNetAction() => new NetToggleAmbushAction { enabled = Enabled };

        public override string ToString() => $"ToggleAmbushAction player: {Player.NetId} enabled: {Enabled}";
    }

    public struct NetToggleAmbushAction : INetAction, IPacketSerializable
    {
        public bool enabled;

        public GameAction ToGameAction(Player player) => new ToggleAmbushAction(player, enabled);

        public void Serialize(PacketWriter writer) => writer.WriteBool(enabled);

        public void Deserialize(PacketReader reader) => enabled = reader.ReadBool();

        public override string ToString() => $"NetToggleAmbushAction enabled: {enabled}";
    }
}
