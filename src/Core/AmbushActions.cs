using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

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
                return;
            }

            CardPile? pile = card.Pile;
            AmbushZoneState zone = AmbushZone.For(Player);
            if (pile == null || pile.Type != PileType.Hand || zone.IsFull || zone.IsCoolingDown
                || Player.PlayerCombatState == null || Player.GetRelic<FrostFang>() == null
                || !card.CanPlay(out UnplayableReason _, out AbstractModel? _))
            {
                Log.Info($"伏击者：埋牌动作条件不再满足（牌堆={pile?.Type}，区域={zone.Count}/{zone.Capacity}，冷却={zone.IsCoolingDown}），跳过");
                return;
            }

            Log.Info($"伏击者：执行埋牌 {card.Id.Entry}（玩家 {Player.NetId}）");
            await AmbushZone.BuryAsync(new GameActionPlayerChoiceContext(this), card, Player);
        }

        public override INetAction ToNetAction() => new NetBuryCardAction { card = NetCard };

        public override string ToString() => $"BuryCardAction card: {NetCard.ToCardModelOrNull()} index: {NetCard.CombatCardIndex}";
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
            Player.GetRelic<FrostFang>()?.OnAmbushModeSynced();
            // 「磨爪」在伏击形态下耗能为 0，形态一翻转就得立刻反映到手牌费用上
            AmbushZone.SyncCardCosts(Player);
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
