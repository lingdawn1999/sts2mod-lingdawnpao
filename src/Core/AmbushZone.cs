using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 单个玩家的伏击区域。联机时两个玩家可能都选芝士雪豹，状态必须按玩家分开存，
    /// 否则一方埋牌会污染另一方的区域。
    /// </summary>
    public sealed class AmbushZoneState
    {
        public AmbushZoneState(Player player)
        {
            Player = player;
        }

        public Player Player { get; }

        /// <summary>
        /// 埋牌存储：用本体 CardPile（PileType.None，不注册进玩家牌堆表），
        /// 这样伏击堆按钮能直接复用本体 NCardPileScreen 展示埋牌与 FIFO 顺序。
        /// 牌经 RemoveFromCurrentPile 移出牌堆后进这里（card.Pile 指向它），
        /// 带出结算后由打牌流程自动归位（有消耗→消耗堆，否则→弃牌堆）。
        /// </summary>
        public CardPile Pile { get; } = new(PileType.None);

        /// <summary>伏击形态（收集/倾泻）。由 ToggleAmbushAction 在所有客户端上同步翻转。</summary>
        public bool IsAmbushMode { get; set; }

        /// <summary>
        /// 伏击区上限 = <see cref="AmbushCapacityPower" /> 当前层数（战斗开始由 FrostFang.BeforeCombatStart 挂上）。
        /// 「扩张」「张网以待」通过 PowerCmd.Apply 给该能力叠层；遗物「暗杀者」的 +1 在战斗开始时
        /// 一次性挂进基础层数里。该能力战斗结束随 PowerModel 默认清理流程消失，下次战斗重新挂——
        /// 所以这里不需要任何清零代码，<see cref="ReturnBuriedCardsAsync" /> 也不必动它。
        /// <para>
        /// ⚠️ 战斗开始前的瞬间（BeforeCombatStart 尚未执行）能力还没挂上——回落到
        /// <see cref="AmbushZone.MaxSize" />，否则 <see cref="IsFull" /> 判定会因为 Capacity=0
        /// 而让一切埋牌尝试被拒。
        /// </para>
        /// </summary>
        public int Capacity
        {
            get
            {
                int amount = (int)(Player.Creature.GetPower<AmbushCapacityPower>()?.Amount ?? 0);
                return amount > 0 ? amount : AmbushZone.MaxSize;
            }
        }

        /// <summary>
        /// 每张埋牌的增益加值（"蓄势"等卡牌提供）。0 = 基础（攻+1力/技+1敏）。
        /// 改变后由 AmbushZone.RecomputeBonusesAsync 全量重算，战斗结束清零。
        /// </summary>
        public int BonusExtra { get; set; }

        /// <summary>当前已应用到角色身上的力量/敏捷总加成（重算模型的差分基准）。</summary>
        public int AppliedStr { get; set; }

        public int AppliedDex { get; set; }

        /// <summary>
        /// 过载（"过载"卡提供）：本回合伏击区内的牌**视为所有类型**，
        /// 即每张牌同时计入攻击牌与技能牌（同时提供力量与敏捷）。
        /// 由 OverloadPower 的回合钩子管理状态机：过载 → 下回合冷却 → 再下回合恢复。
        /// </summary>
        public bool IsOverloaded { get; set; }

        /// <summary>冷却（过载的代价）：整个伏击区失效一回合——不提供增益、不能埋牌、不能带出。</summary>
        public bool IsCoolingDown { get; set; }

        /// <summary>
        /// 本回合是否已触发过「**每回合首次使用能力牌 → 上限+1**」（2026-09-27 新规则）。
        /// 由 <see cref="FrostFang.BeforeHandDraw" /> 每回合重置、
        /// <see cref="FrostFang.AfterCardPlayed" /> 判定并置位（只认手动打出的能力牌）。
        /// </summary>
        public bool PowerCapacityUsedThisTurn { get; set; }

        /// <summary>
        /// **本场战斗中，你的伏击牌造成过伤害的次数**（「十万埋伏」的伤害成长依据）。
        /// 由 <see cref="FrostFang" /> 在 <c>AfterDamageGiven</c> 里累加、进战斗时归零。
        /// 计的是**伤害结算次数**（多段攻击 = 多次），口径与旧「十万埋伏」能力的触发口径一致。
        /// </summary>
        public int AmbushDamageCount { get; set; }

        /// <summary>
        /// 每张埋牌**埋入时的回合号**（`PlayerCombatState.TurnNumber`）。
        /// 用途：判定"这张牌是**跨回合**潜伏过的"还是"当回合才埋的"——
        /// 这是 2026-09-23 埋伏体系大改的地基（详见 <see cref="AmbushZone.IsVeteranAmbush" />）。
        /// 键用引用相等（`AbstractModel` 未重写 Equals，同一张牌的多份拷贝互不干扰）；
        /// 重新埋入会覆盖旧值，所以不存在"带出后再埋还算老兵"的脏数据。
        /// </summary>
        public Dictionary<CardModel, int> BuriedTurn { get; } = new();

        public IReadOnlyList<CardModel> Cards => Pile.Cards;

        public int Count => Pile.Cards.Count;

        public bool IsFull => Count >= Capacity;

        public int CountByType(CardType type)
        {
            int n = 0;
            foreach (CardModel card in Cards)
            {
                if (card.Type == type)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>
    /// 伏击区域的逻辑入口。所有改变状态的操作都由 BuryCardAction / ToggleAmbushAction 驱动，
    /// 这两个动作经 ActionQueueSynchronizer 入队，会在每个客户端上按同一顺序执行，因此联机不会脱同步；
    /// 带出则发生在遗物 AfterCardPlayed 钩子里（本体出牌动作内部，本来就全端执行）。
    /// </summary>
    public static class AmbushZone
    {
        public const int MaxSize = 3;

        private static readonly Dictionary<ulong, AmbushZoneState> Zones = new();

        // 当前正从伏击区域带出打出的牌（AutoPlay await 期间存在），供"埋伏"两层效果判定
        private static readonly HashSet<CardModel> PlayingFromAmbush = new();

        // 当前正被带出打出、且**满足跨回合条件**的牌（`IsAmbushPlay` 的子集），供"埋伏收益"判定
        private static readonly HashSet<CardModel> VeteranAmbush = new();

        public static AmbushZoneState For(Player player)
        {
            // Player 是运行级实例（跨战斗复用）；实例变了 = 新对局，必须丢弃旧状态。
            // 从主菜单放弃对局不经过 EndCombatInternal，AfterCombatEnd 不会触发，
            // 旧埋牌若不清掉会泄漏进新对局（旧 CardModel 的 CombatState 已死，一带出就 NRE）。
            if (!Zones.TryGetValue(player.NetId, out AmbushZoneState? zone) || !ReferenceEquals(zone.Player, player))
            {
                if (zone != null)
                {
                    Log.Info($"伏击区域：检测到新对局（玩家 {player.NetId}），丢弃旧状态（{zone.Count} 张埋牌，伏击形态={zone.IsAmbushMode}）");
                    PlayingFromAmbush.Clear();
                    VeteranAmbush.Clear();
                }

                zone = new AmbushZoneState(player);
                Zones[player.NetId] = zone;
            }

            return zone;
        }

        /// <summary>
        /// 本地玩家。RunManager.State 是私有的，改从战斗房间的角色节点拿战斗状态再按 NetId 取。
        /// </summary>
        public static Player? LocalPlayer
        {
            get
            {
                ulong? netId = LocalContext.NetId;
                NCombatRoom? room = NCombatRoom.Instance;
                if (!netId.HasValue || room == null)
                {
                    return null;
                }

                foreach (NCreature node in room.CreatureNodes)
                {
                    ICombatState? state = node.Entity?.CombatState;
                    Player? player = state?.GetPlayer(netId.Value);
                    if (player != null)
                    {
                        return player;
                    }
                }

                return null;
            }
        }

        /// <summary>本地玩家的伏击区域（战斗 UI 只展示自己那份）。不在战斗里时为 null。</summary>
        public static AmbushZoneState? Local
        {
            get
            {
                Player? me = LocalPlayer;
                return me == null ? null : For(me);
            }
        }

        /// <summary>
        /// 是否**任意**伏击玩家的手牌里躺着「伏击姿态」（手牌上限排除它的判据，见
        /// <see cref="AmbushStance.HandLimit" />）。
        /// <para>
        /// ⚠️ **必须是"任意持有者"这种全端一致的判据**：`CardPile.MaxCardsInHand` 是无参静态属性，
        /// 调用点拿不到"这张牌属于谁"，按本地玩家判定会让联机两端各算各的（一端 11 一端 10）→
        /// 抽牌张数脱同步。按"本场战斗里有没有人持有姿态牌"判定则两端完全一致。
        /// </para>
        /// </summary>
        public static bool AnyHandHoldsFormCard()
        {
            foreach (AmbushZoneState zone in Zones.Values)
            {
                CardPile? hand = zone.Player.PlayerCombatState?.Hand;
                if (hand == null)
                {
                    continue;
                }

                foreach (CardModel card in hand.Cards)
                {
                    if (card is AmbushStance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsAmbushPlay(CardModel card) => PlayingFromAmbush.Contains(card);

        /// <summary>
        /// **「埋伏」收益的唯一判定**（2026-09-23 埋伏体系大改）：这张牌不但是被带出打出的，
        /// 而且**在伏击区域停留过至少一个回合**。
        /// <para>
        /// **为什么要有这一层**：改前所有埋伏卡都只看 <see cref="IsAmbushPlay" />，而右键切换形态是
        /// **完全免费、无冷却、一回合内可反复切**的，带出打出本身也不再付费（埋时已付）——
        /// 于是一回合内"埋进去 → 切回正常 → 打一张别的牌把它带出来"就能零成本白嫖埋伏收益，
        /// 名义上的代价（占一格 + 延后一回合兑现）被切换形态当场作废。用户 2026-09-23 判定
        /// "很多牌都是埋伏进池子里然后推出伏击马上打出来白嫖埋伏收益"，要求大改。
        /// </para>
        /// <para>
        /// **改法**：埋伏 = 真正的"潜伏"——同回合埋入同回合带出只走基础效果，跨回合带出才吃埋伏收益。
        /// 操作自由度不变（仍可同回合埋了马上带出，只是不再白拿收益），代价变成
        /// **真等一回合 + 占 1/3 格 + 该牌当回合不可用**，埋伏数值因此得以整体上调。
        /// </para>
        /// <para>
        /// ⚠️ **不要**把带出牌的其它收益（「追猎」锁头加成、「连扑」额外打一遍、
        /// 「霜牙」吞区加伤、冰封王座的记账）也换成这个谓词——那些是"**带出**"的收益，不是
        /// "**埋伏**"词条的收益，本次大改刻意不动它们。
        /// </para>
        /// </summary>
        public static bool IsVeteranAmbush(CardModel card) => VeteranAmbush.Contains(card);

        /// <summary>
        /// 这张牌此刻**躺在伏击区域里、已潜伏满一回合、且带「埋伏」词条**——
        /// 也就是"带出来就能吃到埋伏收益"。
        /// 供伏击堆界面给它**亮金框**（2026-09-23 用户要求：取消原来的"未就绪降暗"，
        /// 改成"能触发埋伏的牌亮金框、其余一律正常显示"）。
        /// 「带埋伏词条」由标记接口 <see cref="IAmbushBonusCard" /> 判定，见那个接口的维护约定。
        /// </summary>
        public static bool IsReadyForAmbush(CardModel? card)
        {
            if (card is not IAmbushBonusCard)
            {
                return false;
            }

            foreach (AmbushZoneState zone in Zones.Values)
            {
                if (zone.Pile.Cards.Contains(card))
                {
                    return IsReady(zone, card);
                }
            }

            return false;
        }

        /// <summary>
        /// 埋牌是否已"潜伏满一回合"（埋入时的回合号 ≠ 当前回合号）。
        /// <para>
        /// 「野生狗奶」（<see cref="WildDogMilkPower" />，显示名「永恒」）在场时**短路这个条件**：
        /// 埋入当回合即可带出并吃埋伏收益（用户 2026-09-23 定稿的"付费解锁"）。
        /// 前置的 `BuriedTurn` 命中判定保留——牌必须真的躺在区域里（`IsReadyForAmbush` 还会再查一次）。
        /// </para>
        /// <para>
        /// ⚠️ **只免等待、不碰冷却**：冷却拦的是 `BuryAsync` 与带出入口，与本判定无关。
        /// </para>
        /// </summary>
        private static bool IsReady(AmbushZoneState zone, CardModel card) =>
            zone.BuriedTurn.TryGetValue(card, out int buriedTurn)
            && zone.Player.PlayerCombatState != null
            && (buriedTurn != zone.Player.PlayerCombatState.TurnNumber
                || zone.Player.Creature.GetPower<WildDogMilkPower>() != null);

        /// <summary>记下这张牌的埋入回合（`IsReady` 的判定依据）。</summary>
        private static void MarkBuried(AmbushZoneState zone, CardModel card)
        {
            if (zone.Player.PlayerCombatState != null)
            {
                zone.BuriedTurn[card] = zone.Player.PlayerCombatState.TurnNumber;
            }
        }

        /// <summary>
        /// 埋伏效果的触发次数 = 1 + 「伏兵」能力层数。
        /// 所有带埋伏效果的卡都必须用它包住自己的**埋伏分支**（基础效果不重复触发）。
        /// </summary>
        public static int AmbushTriggerCount(Player player) =>
            1 + (player.Creature.GetPower<AmbushEchoPower>()?.ExtraTriggers ?? 0);

        /// <summary>
        /// 本场战斗中该玩家的伏击牌造成过伤害的次数（「十万埋伏」的动态伤害用）。
        /// 只读查询：不建条目，所以**战斗外（牌组/收藏界面预览卡面）也安全**，返回 0。
        /// </summary>
        public static int AmbushDamageCount(Player player) =>
            Zones.TryGetValue(player.NetId, out AmbushZoneState? zone) ? zone.AmbushDamageCount : 0;

        /// <summary>
        /// 埋牌：把牌从手牌移进伏击区域、给区域层数与攻/防加成。
        /// 手牌埋入走 BuryCardAction.ExecuteAction（全端执行同一份逻辑）；生成牌走 BuryGeneratedAsync。
        /// `spendResources: false` 用于遗物驱动的免费埋入（「暗杀者」的回合开始自选），不扣能量/辉星。
        /// </summary>
        public static async Task BuryAsync(PlayerChoiceContext choiceContext, CardModel card, Player player, bool spendResources = true)
        {
            AmbushZoneState zone = For(player);
            if (zone.IsFull || zone.IsCoolingDown || player.PlayerCombatState == null)
            {
                return;
            }

            if (spendResources)
            {
                // 埋牌同样要付费，冰封王座的"贷款"也在这里生效（能量不足时 hook 把耗能改成 0）。
                // 但埋牌不走 AfterCardPlayed，自伤必须在这里补结算——否则贷款埋牌等于零代价
                // （2026-09-23 用户报障）。返回值第 1 项 = 实际应付/花掉的能量，只有它真的是 0
                // （贷款生效）才结算；非 0 说明是"付得起、按原价埋"。
                (int energySpent, int _) = await card.SpendResources();
                if (energySpent == 0)
                {
                    FrostThronePower? throne = player.Creature.GetPower<FrostThronePower>();
                    if (throne != null)
                    {
                        await throne.SettleBuriedCostAsync(choiceContext, card);
                    }
                }
            }
            AmbushZoneUi.Prepare();
            // 官方"手动出牌"流程负责把手牌 holder 正确释放（直接 RemoveFromCurrentPile 会让卡面残留），
            // 但它会把牌塞进出牌堆并启动一个飞向屏幕中心的 tween。
            await CardPileCmd.AddDuringManualCardPlay(card);
            // 必须在 RemoveFromCurrentPile 之前找节点：CardModel.Pile 是"在 owner.Piles 里找包含自己的堆"
            // 的计算属性，移出后 card.Pile 为 null，FindOnTable 直接返回 null，视觉就永远不执行。
            NCard? node = NCard.FindOnTable(card, PileType.Play);
            card.RemoveFromCurrentPile();
            if (node != null && NCombatRoom.Instance != null)
            {
                node.PlayPileTween?.Kill();
                node.PlayPileTween = null;
                // 节点现在挂在 PlayContainer 里（AddDuringManualCardPlay 把它搬进去了），
                // 我们要让它飞出容器去飞伏击堆按钮 → 先移出容器挂到 Ui 根上，保持当前世界坐标不变。
                Vector2 worldPos = node.GlobalPosition;
                Control parent = node.GetParent<Control>();
                parent?.RemoveChildSafely(node);
                NCombatRoom.Instance.Ui.AddChildSafely(node);
                node.GlobalPosition = worldPos;

                NCreature? creatureNode = NCombatRoom.Instance.GetCreatureNode(player.Creature);
                Vector2 destination = AmbushZoneUi.GetCenter() ?? creatureNode?.GlobalPosition ?? node.GlobalPosition;
                Tween tween = node.CreateTween().SetParallel();
                tween.TweenProperty(node, "global_position", destination, 0.3f)
                    .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
                tween.TweenProperty(node, "scale", Vector2.One * 0.4f, 0.3f)
                    .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
                tween.TweenProperty(node, "modulate", StsColors.transparentBlack, 0.3f);
                tween.Chain().TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(node))
                    {
                        NCombatRoom.Instance.Ui.AddChildSafely(ExhaustVfxCompat.CreateVfx(node));
                        node.QueueFreeSafely();
                    }
                }));
                tween.Play();
                // 不 await：让动画 fire-and-forget，避免阻塞动作队列 0.3s
            }
            else if (node == null)
            {
                Log.Info("伏击区域：未找到埋牌卡面节点，跳过飞入动画");
            }

            zone.Pile.AddInternal(card);
            MarkBuried(zone, card);
            Log.Info($"伏击区域：玩家 {player.NetId} 埋入 {card.Id.Entry}（当前 {zone.Count}/{zone.Capacity}）");
            await NotifyCountChangedAsync(choiceContext, player, 1);
            // 永恒狩猎（金卡能力）：只有**手动埋**（spendResources: true，要付费的那种）才占
            // "本回合免费埋牌"额度——额度存在的意义就是免掉这笔费用，效果埋入
            // （暗杀者遗物 /「雪原疾行」等 spendResources: false 的路径）本来就不花钱，
            // 不该白吃额度（2026-09-27 用户指出）。生成牌埋入（BuryGeneratedAsync）本就不经这里。
            if (spendResources)
            {
                player.Creature.GetPower<EternalHuntPower>()?.NotifyBury();
            }
            // 能力牌埋入不提供额外收益：伏击区只有攻击牌（+力量）与技能牌（+敏捷）两类收益

            await RecomputeBonusesAsync(choiceContext, player);
            await SyncPowerAsync(choiceContext, player);
            // 伏击本能（金卡能力）：本回合**首次**把牌加入区域 → 获得 1 点能量（每回合只结算一次，堆多少张都一样）
            await KillerInstinctPower.NotifyBuriedAsync(choiceContext, player);
            // 暗夜收割已改为"每回合开始看区域是否非空"（见 NightPatrolPower），埋入不再结算
            AmbushZoneUi.Refresh();
        }

        /// <summary>
        /// 生成牌直接埋入（"设伏"等生成卡使用）：牌由 combatState.CreateCard 当场创建，从未进过手牌，
        /// 不扣费、不走手动出牌视觉。战后随战斗临时牌组一并销毁（若未被带出，ReturnBuriedCardsAsync
        /// 会把它移到战斗弃牌堆，同样不进 master deck）。
        /// </summary>
        public static async Task<bool> BuryGeneratedAsync(PlayerChoiceContext choiceContext, Player player, CardModel card, string source = "生成牌")
        {
            AmbushZoneState zone = For(player);
            if (zone.IsFull || zone.IsCoolingDown)
            {
                return false;
            }

            zone.Pile.AddInternal(card);
            MarkBuried(zone, card);
            Log.Info($"伏击区域：{card.Id.Entry} 埋入（来源：{source}，当前 {zone.Count}/{zone.Capacity}）");
            await NotifyCountChangedAsync(choiceContext, player, 1);
            await RecomputeBonusesAsync(choiceContext, player);
            await SyncPowerAsync(choiceContext, player);
            // 伏击本能（金卡能力）：本回合**首次**把牌加入区域 → 获得 1 点能量（每回合只结算一次，堆多少张都一样）
            await KillerInstinctPower.NotifyBuriedAsync(choiceContext, player);
            // 暗夜收割已改为"每回合开始看区域是否非空"（见 NightPatrolPower），埋入不再结算
            AmbushZoneUi.Refresh();
            return true;
        }

        /// <summary>
        /// 「扑击」专用（2026-09-23 用户定稿的效果：造成 3 点伤害，把**抽牌堆顶**的牌放进伏击区）。
        /// 与 <see cref="BuryGeneratedAsync" /> 的唯一区别是"牌从哪来"——那张是新造的、从未进过任何牌堆，
        /// 这张要从抽牌堆里**取出来**，所以必须先 `RemoveFromCurrentPile()` 再入区
        /// （同一张牌不能同时属于两个牌堆，`CardPile.AddInternal` 见到已在堆里的牌会直接抛异常）。
        /// **抽牌堆顶 = `Cards[0]`**：本体 `CardPileCmd.Draw` 取的就是 `drawPile.Cards.FirstOrDefault()`（已核对源码）。
        /// 抽牌堆空 / 区域已满 / 区域冷却时埋不进，返回 false —— 调用方据此放弃，本牌就只结算那 3 点伤害
        /// （照「设伏」「捕猎」的既有约定：不满足条件即空放，不做额外保护）。
        /// </summary>
        public static async Task<bool> BuryTopOfDrawPileAsync(PlayerChoiceContext choiceContext, Player player)
        {
            AmbushZoneState zone = For(player);
            if (zone.IsFull || zone.IsCoolingDown)
            {
                return false;
            }

            CardModel? card = PileType.Draw.GetPile(player).Cards.FirstOrDefault();
            if (card == null)
            {
                Log.Info("伏击区域：扑击想埋抽牌堆顶，但抽牌堆是空的，跳过");
                return false;
            }

            card.RemoveFromCurrentPile();
            return await BuryGeneratedAsync(choiceContext, player, card, "抽牌堆顶");
        }

        /// <summary>
        /// 从**弃牌堆**回收一张牌进伏击区（「循迹」回合开始自动埋入用）。
        /// 与 <see cref="BuryTopOfDrawPileAsync" /> 同构：牌本来就在别的牌堆里，
        /// 必须先 `RemoveFromCurrentPile()` 再走 <see cref="BuryGeneratedAsync" /> 的公共记账
        /// （同一张牌不能同时属于两个牌堆，`CardPile.AddInternal` 见到已在堆里的牌会直接抛异常）。
        /// 区域已满 / 冷却中 → 返回 false，牌老实留在弃牌堆。
        /// </summary>
        public static async Task<bool> BuryFromDiscardAsync(PlayerChoiceContext choiceContext, Player player, CardModel card)
        {
            AmbushZoneState zone = For(player);
            if (zone.IsFull || zone.IsCoolingDown)
            {
                return false;
            }

            if (card.Pile?.Type != PileType.Discard)
            {
                Log.Info($"伏击区域：想把 {card.Id.Entry} 从弃牌堆收回，但它不在弃牌堆（{card.Pile?.Type.ToString() ?? "null"}），跳过");
                return false;
            }

            card.RemoveFromCurrentPile();
            return await BuryGeneratedAsync(choiceContext, player, card, "弃牌堆回收");
        }

        /// <summary>
        /// "打出时既带出旧牌、又往区域里生成新牌"的**生成件**（「设伏」「捕猎」「永恒呼吸」「扑击」）
        /// 的统一结算顺序：① 先看伏击区现状 → ② 把**打出前就在区域里**的旧牌按 FIFO 带出 → ③ 最后才生成新牌。
        /// <para>
        /// **为什么由卡自己发起、而不是交给遗物**：遗物 <see cref="FrostFang.AfterCardPlayed" /> 挂在出牌
        /// **之后**，那一刻新牌已经躺在区域里了——它照 FIFO 取"最早的那张"，区域原本为空时带出的就是
        /// 这张卡自己刚生成的暗袭/猎物/呼吸/真牌（2026-09-22 用户报障"这个尽量别带"）。当时为此把整张卡
        /// 豁免，代价是**连正常的旧牌也一起带不出来了**（2026-09-23 用户报障"修改设伏之后，设伏不能正常
        /// 带出普通卡了"）。改成"卡内先带出 + 遗物整张豁免"后两个诉求同时成立：旧牌照常兑现，新牌老实留下。
        /// </para>
        /// <para>
        /// 伏击形态下不做事（区域在**收集**，不是倾泻；何况此时出牌本来就被拦成埋牌）。
        /// 区域为空 / 冷却中由 <see cref="BringOutFirstAsync" /> 自行返回 null，这里不必判。
        /// 自动打出（被带出的埋伏牌经 AutoPlay 打出）同样生效——那时它就是一张普通牌，走同一套顺序。
        /// </para>
        /// </summary>
        public static async Task BringOutBeforeGenerateAsync(PlayerChoiceContext choiceContext, Creature? target, Player player)
        {
            if (!For(player).IsAmbushMode)
            {
                await BringOutFirstAsync(choiceContext, target, player);
            }
        }

        /// <summary>带出最前的一张伏击牌并结算。返回被带出的牌（区域为空时返回 null），供调用方按类型决定后续。</summary>
        public static async Task<CardModel?> BringOutFirstAsync(PlayerChoiceContext choiceContext, Creature? target, Player player)
        {
            AmbushZoneState zone = For(player);
            if (zone.Pile.Cards.Count == 0 || zone.IsCoolingDown)
            {
                return null;
            }

            CardModel card = zone.Pile.Cards[0];
            // 「埋伏」收益的资格必须**在移出区域之前**读出来（`IsReady` 依赖埋入回合记账）
            bool veteran = IsReady(zone, card);
            zone.Pile.RemoveInternal(card);
            zone.BuriedTurn.Remove(card);
            // 「追猎」：场上有被追猎的敌人时，带出的牌一律改打它（否则跟随本回合打出那张牌的目标）
            target = TrackedPower.FindTrackedTarget(player) ?? target;
            // 目标已经不在了（被上一张带出的牌打死 / 追猎目标死亡 / 原本就没指定目标）：
            // **改打随机一名存活敌人，而不是对着尸体继续打**（2026-09-22 用户报障"不要鞭尸"）。
            if (target == null || !target.IsAlive)
            {
                IReadOnlyList<Creature>? enemies = player.Creature.CombatState?.HittableEnemies;
                target = enemies is { Count: > 0 } ? player.RunState.Rng.CombatTargets.NextItem(enemies) : null;
            }
            Log.Info($"伏击区域：玩家 {player.NetId} 带出 {card.Id.Entry}（剩余 {zone.Count}，"
                + (veteran ? "已潜伏满一回合 → 吃埋伏收益" : "本回合才埋 → 只走基础效果") + "）");
            await NotifyCountChangedAsync(choiceContext, player, -1);
            Creature creature = player.Creature;
            AmbushZoneUi.Refresh();
            // 带出即减层：能力层数必须与区域现内容一致（曾经只加不减，埋→带出→再埋会攒到超出上限的数字）
            await SyncPowerAsync(choiceContext, player);
            // 伏击本能（金卡能力）：本回合**首次**带出牌 → 抽等同层数的牌（每回合只结算一次，
            // 「倾泄」一次带出一串也只算第一张——防刷闸门，见 KillerInstinctPower）
            await KillerInstinctPower.NotifyBroughtOutAsync(choiceContext, player);

            // 带出牌视觉完全自理：skipCardPileVisuals 让引擎全程不生成/不移动卡面
            // （否则本体会在出牌区中心凭空生成一张并停在那里整个结算期，就是用户嫌丑的"卡在中间"），
            // 节点由我们预生成：从伏击堆按钮飞到角色上方悬停结算，结算完飞向结果堆缩小消散。
            NCard? node = null;
            // 视觉只对本地玩家的带出播放：锚点取的是**本地**的伏击堆按钮与本地牌堆按钮，
            // 队友带出时若照播，会把牌"从我屏幕上的按钮飞向队友"。逻辑仍然全端执行。
            if (!TestMode.IsOn && NCombatRoom.Instance != null && LocalContext.IsMe(player))
            {
                node = NCard.Create(card);
                if (node != null)
                {
                    NCombatRoom.Instance.Ui.AddToPlayContainer(node);
                    node.UpdateVisuals(PileType.Play, CardPreviewMode.Normal);
                    NCreature? creatureNode = NCombatRoom.Instance.GetCreatureNode(creature);
                    Vector2 from = AmbushZoneUi.GetCenter() ?? creatureNode?.GlobalPosition ?? node.GlobalPosition;
                    node.GlobalPosition = from - node.Size * 0.5f;
                    Vector2 hover = creatureNode != null
                        ? creatureNode.GlobalPosition + Vector2.Up * 150f
                        : from + Vector2.Left * 190f;
                    Tween flyIn = node.CreateTween();
                    flyIn.TweenProperty(node, "global_position", hover - node.Size * 0.5f, 0.3f)
                        .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                }
            }

            // 免费打出（埋的时候已付过能量）。
            // 打出期间标记为"埋伏打出"，卡牌 OnPlay 据此使用第二层效果。
            // 「连扑」：这张带出牌若会额外打出一遍，结算后要消耗掉。
            // 层数在打出过程中会被扣掉，所以必须在 AutoPlay 之前读。
            bool chainExhaust = player.Creature.GetPower<ChainPouncePower>()?.Amount > 0;
            PlayingFromAmbush.Add(card);
            if (veteran)
            {
                VeteranAmbush.Add(card);
            }

            try
            {
                await CardCmd.AutoPlay(choiceContext, card, target, skipCardPileVisuals: true);
            }
            finally
            {
                PlayingFromAmbush.Remove(card);
                VeteranAmbush.Remove(card);
            }

            // 「连扑」：这张带出牌若会额外打出一遍，结算后要消耗掉。
            if (chainExhaust)
            {
                // 同样补发牌堆计数事件（这张牌是跳过视觉进消耗堆的，本体不会替我们发）
                await ExhaustCardWithoutVisualsAsync(choiceContext, card, player);
            }

            if (node != null && GodotObject.IsInstanceValid(node))
            {
                if (NCombatRoom.Instance != null)
                {
                    NCombatUi ui = NCombatRoom.Instance.Ui;
                    Control? pileButton = card.Pile?.Type switch
                    {
                        PileType.Discard => ui.DiscardPile,
                        PileType.Exhaust => ui.ExhaustPile,
                        _ => null,
                    };
                    Vector2 to = pileButton != null
                        ? pileButton.GlobalPosition + pileButton.Size * 0.5f
                        : node.GlobalPosition;
                    Tween putAway = node.CreateTween().SetParallel();
                    putAway.TweenProperty(node, "global_position", to, 0.3f)
                        .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
                    putAway.TweenProperty(node, "scale", Vector2.One * 0.5f, 0.3f)
                        .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
                    putAway.Chain().TweenCallback(Callable.From(node.QueueFreeSafely));
                }
                else
                {
                    node.QueueFreeSafely();
                }
            }

            // 带出的牌结算完毕后，才按区域现内容重算加成（此时该牌已移出，
            // 重算会收回它的力量/敏捷份额），保证带出的牌结算时仍享受加成。
            await RecomputeBonusesAsync(choiceContext, player);
            return card;
        }

        public static async Task BringOutAllAsync(PlayerChoiceContext choiceContext, Creature? target, Player player)
        {
            while (For(player).Pile.Cards.Count > 0)
            {
                // 冷却中 BringOutFirstAsync 会返回 null，必须跳出，否则死循环
                if (await BringOutFirstAsync(choiceContext, target, player) == null)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 按区域当前内容全量重算力量/敏捷加成（差分应用，只补差额）。
        /// 每张攻击牌 = +（1+BonusExtra）力量，技能牌 = +（1+BonusExtra）敏捷。
        /// 埋入/带出/改变 BonusExtra 后都必须调用；动作队列保证全端一致。
        /// </summary>
        public static async Task RecomputeBonusesAsync(PlayerChoiceContext choiceContext, Player player)
        {
            AmbushZoneState zone = For(player);
            CountZoneTypes(zone, out int attacks, out int skills);

            int perCard = 1 + zone.BonusExtra;
            // 冷却中（过载的代价 / 凝神）：伏击区不提供任何力/敏增益
            int wantStr = zone.IsCoolingDown ? 0 : attacks * perCard;
            int wantDex = zone.IsCoolingDown ? 0 : skills * perCard;
            Creature creature = player.Creature;
            if (wantStr != zone.AppliedStr)
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, creature, wantStr - zone.AppliedStr, creature, null);
                zone.AppliedStr = wantStr;
            }

            if (wantDex != zone.AppliedDex)
            {
                await PowerCmd.Apply<DexterityPower>(choiceContext, creature, wantDex - zone.AppliedDex, creature, null);
                zone.AppliedDex = wantDex;
            }

            // 「有备而来」「磨爪」的费用随伏击状态变化，区域每次变化都同步一次（升降都支持）
            SyncCardCosts(player);
        }

        /// <summary>
        /// 按区域当前内容数出**攻击牌 / 技能牌**张数（过载时每张牌同时计入两类）。
        /// 两个用点：<see cref="RecomputeBonusesAsync" />（再乘「蓄势」的 `1 + BonusExtra` 换算力敏）与
        /// 「集群伏击」（按**基础 1 点/张**发临时增益给队友，**刻意不含蓄势加值**——用户 2026-09-25 澄清）。
        /// </summary>
        public static void CountZoneTypes(AmbushZoneState zone, out int attacks, out int skills)
        {
            attacks = 0;
            skills = 0;
            foreach (CardModel card in zone.Cards)
            {
                // 过载：本回合区内牌视为所有类型（同时计入攻击与技能）
                if (zone.IsOverloaded)
                {
                    attacks++;
                    skills++;
                    continue;
                }

                switch (card.Type)
                {
                    case CardType.Attack:
                        attacks++;
                        break;
                    case CardType.Skill:
                        skills++;
                        break;
                }
            }
        }

        /// <summary>
        /// 同步"费用随伏击状态变化"的卡牌（「有备而来」「守住」按区域张数、「磨爪」按伏击形态）。
        /// 两个调用点：区域每次变化（RecomputeBonusesAsync 末尾）与右键切换伏击形态（ToggleAmbushAction）。
        /// </summary>
        public static void SyncCardCosts(Player player)
        {
            ReadyForAnything.SyncCosts(player);
            HoldFast.SyncCosts(player);
        }

        /// <summary>
        /// 把「伏击区域」能力层数同步为区域当前埋牌数（差分应用，纯显示不携带行为）。
        /// 与 RecomputeBonusesAsync 同一套重算模型：读现有层数、只补差额。
        /// 埋入 / 带出 / 战斗结束归还后都必须调用——本能力原先只在埋牌时 +1、带出时从不减层，
        /// 层数只涨不跌会累积到超出区域上限（埋→带出→再埋就看到 4 层而区内只有 1 张）。
        /// </summary>
        public static async Task SyncPowerAsync(PlayerChoiceContext choiceContext, Player player)
        {
            AmbushZoneState zone = For(player);
            Creature creature = player.Creature;
            AmbushZonePower? power = creature.GetPower<AmbushZonePower>();
            int current = power == null ? 0 : (int)power.Amount;
            int want = zone.Count;
            if (want != current)
            {
                // Amount 归 0 时引擎会自行移除该能力（ShouldRemoveDueToAmount），区域空了图标即消失
                await PowerCmd.Apply<AmbushZonePower>(choiceContext, creature, want - current, creature, null);
                power = creature.GetPower<AmbushZonePower>();
            }

            power?.Pulse();
        }

        /// <summary>
        /// 清空伏击区域，把里面的牌全部置入消耗牌堆（"重整计划""收网"用），返回被清掉的张数。
        /// 不触发任何埋伏效果、不走带出打出的流程——那是「倾泄」和带出路径的活。
        /// **本方法不重算增益**：调用方要在结算完自身效果后再调 <see cref="RefreshZoneAsync"/>，
        /// 与带出路径同理——让兑现的牌在结算期间仍享受区域提供的 +力量/+敏捷。
        /// </summary>
        public static async Task<int> ExhaustAllAsync(PlayerChoiceContext choiceContext, Player player)
        {
            AmbushZoneState zone = For(player);
            int count = zone.Pile.Cards.Count;
            if (count == 0)
            {
                return 0;
            }

            // 同本体 CardCmd.Exhaust 的注释：不要做批量版，前一张的钩子要跑完再处理下一张
            while (zone.Pile.Cards.Count > 0)
            {
                CardModel card = zone.Pile.Cards[0];
                zone.Pile.RemoveInternal(card);
                zone.BuriedTurn.Remove(card);
                Log.Info($"伏击区域：玩家 {player.NetId} 消耗埋牌 {card.Id.Entry}");
                // 区域里的牌不在桌面节点上，跳过飞入消耗堆的视觉，避免 tween 找不到起点；
                // 角标由 ExhaustCardWithoutVisualsAsync 手动补发事件（详见那个方法的注释）
                await ExhaustCardWithoutVisualsAsync(choiceContext, card, player);
            }

            await NotifyCountChangedAsync(choiceContext, player, -count);
            return count;
        }

        /// <summary>
        /// 只消耗伏击区域**最前面（最先加入）**的一张牌，返回那张牌（区域为空时返回 null）。
        /// 「霜牙」用：2026-09-25 用户把"吞掉整片区域"改成"只吞首张"，于是它只吃第一张牌的伤害。
        /// 与 <see cref="ExhaustAllAsync" /> 同一约定：**本方法不重算增益**，调用方结算完自身效果后再调
        /// <see cref="RefreshZoneAsync" />——让这张牌本身的 +力量/+敏捷在结算期间仍然生效。
        /// </summary>
        public static async Task<CardModel?> ExhaustFirstAsync(PlayerChoiceContext choiceContext, Player player)
        {
            AmbushZoneState zone = For(player);
            if (zone.Pile.Cards.Count == 0)
            {
                return null;
            }

            CardModel card = zone.Pile.Cards[0];
            zone.Pile.RemoveInternal(card);
            zone.BuriedTurn.Remove(card);
            Log.Info($"伏击区域：玩家 {player.NetId} 消耗首张埋牌 {card.Id.Entry}");
            // 区域里的牌不在桌面节点上，跳过视觉 + 手动补发事件（详见 ExhaustCardWithoutVisualsAsync）
            await ExhaustCardWithoutVisualsAsync(choiceContext, card, player);
            await NotifyCountChangedAsync(choiceContext, player, -1);
            return card;
        }

        /// <summary>
        /// 让玩家**自选**伏击区域里的一张牌（「霜牙」烧牌用），返回被选中的那张；区域为空返回 null。
        /// <para>
        /// ⚠️ 用 <c>CardSelectCmd.FromSimpleGrid</c>（收一串 CardModel 的通用选牌入口）而不是
        /// `FromCombatPile`：区域堆是 `PileType.None`，而 `FromCombatPile` 开头就
        /// `if (!pile.IsCombatPile) throw`（本体 CardSelectCmd.cs:386）——非战斗堆直接用会被打回。
        /// `FromSimpleGrid` 自带联机选牌同步（ReserveChoiceId / SyncLocalChoice / WaitForRemoteChoice），
        /// 所以联机下两端不会因为"只有一端出了选牌界面"而分叉。
        /// </para>
        /// </summary>
        /// <param name="prompt">选牌界面的提示语（由调用方给：卡牌传 <c>SelectionScreenPrompt</c>）。</param>
        public static async Task<CardModel?> SelectOneAsync(PlayerChoiceContext choiceContext, Player player, LocString prompt)
        {
            List<CardModel> candidates = For(player).Cards.ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            IEnumerable<CardModel> picked = await CardSelectCmd.FromSimpleGrid(
                choiceContext, candidates, player, new CardSelectorPrefs(prompt, 1, 1));
            return picked.FirstOrDefault();
        }

        /// <summary>
        /// 把伏击区域里的**指定一张**牌烧进消耗堆，返回是否真烧掉了（区域里已经没有它就返回 false）。
        /// 与 <see cref="ExhaustAllAsync" /> 同一约定：**本方法不重算增益**，调用方结算完自身效果后再调
        /// <see cref="RefreshZoneAsync" />——让这张牌本身的 +力量/+敏捷在结算期间仍然生效。
        /// 打法照抄 <see cref="ExhaustAllAsync" /> 逐张那条路：区域里的牌不在桌面节点上，跳过视觉 + 手动补发入堆事件。
        /// </summary>
        public static async Task<bool> ExhaustChosenAsync(PlayerChoiceContext choiceContext, Player player, CardModel card)
        {
            AmbushZoneState zone = For(player);
            if (!zone.Pile.Cards.Contains(card))
            {
                return false;
            }

            zone.Pile.RemoveInternal(card);
            zone.BuriedTurn.Remove(card);
            Log.Info($"伏击区域：玩家 {player.NetId} 消耗自选埋牌 {card.Id.Entry}");
            await ExhaustCardWithoutVisualsAsync(choiceContext, card, player);
            await NotifyCountChangedAsync(choiceContext, player, -1);
            return true;
        }

        /// <summary>
        /// **跳过视觉地消耗一张牌，并补发「牌已入堆」事件**。
        /// <para>
        /// ⚠️ **为什么要手动补发**：本体的 `CardPileCmd.Add` 只在**有卡牌节点的视觉分支**里才调用
        /// `CardPile.InvokeCardAddFinished()`（那一行在 `cardNodes.Count != 0` 的块内），
        /// 而消耗堆按钮角标（`NCombatCardPile.AddCard`）正是靠这个事件 +1 的。
        /// 伏击区域里的牌**本来就没有桌面节点**（`NCard.FindOnTable` 取不到），所以这里一律
        /// `skipVisuals: true` —— 结果就是牌真的进了消耗堆（界面里看得见），但**外面角标不涨**。
        /// 用户 2026-09-25 报障：「重整计划」消耗 4 张，消耗堆却仍显示 1 张。
        /// </para>
        /// <para>
        /// 补发一次即可：`NCombatCardPile.AddCard()` 内部是 `Min(当前显示值 + 1, 实际张数)`，
        /// 所以每张补发一次刚好对上；即使多补也不会溢出（上限被实际张数夹住）。
        /// 只影响本地 UI（事件的其他订阅者是本地的牌堆按钮），联机下对端没有订户、无副作用。
        /// </para>
        /// </summary>
        private static async Task ExhaustCardWithoutVisualsAsync(PlayerChoiceContext choiceContext, CardModel card, Player player)
        {
            await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false, skipVisuals: true);
            PileType.Exhaust.GetPile(player).InvokeCardAddFinished();
        }

        /// <summary>
        /// 「陷阱回收」：把区域里的牌全部**放回手牌**（与 <see cref="ExhaustAllAsync"/> 相对的那条路）。
        /// 不送回它们的原始堆——区域里的牌本来就是从手牌埋进去的，回手才符合"回收"的语义，
        /// 而且玩家能重新埋一次。区域因此清空，换回收益的那部分 +力量/+敏捷也随之消失。
        /// 与 ExhaustAllAsync 同样的约定：**本方法不重算增益**，调用方结算完自身效果后再调 <see cref="RefreshZoneAsync"/>。
        /// 逐张处理（同 CardCmd.Exhaust 的注释：前一张的钩子要跑完再动下一张）。
        /// </summary>
        public static async Task<int> ReturnAllToHandAsync(PlayerChoiceContext choiceContext, Player player)
        {
            AmbushZoneState zone = For(player);
            int count = zone.Pile.Cards.Count;
            if (count == 0)
            {
                return 0;
            }

            while (zone.Pile.Cards.Count > 0)
            {
                CardModel card = zone.Pile.Cards[0];
                zone.Pile.RemoveInternal(card);
                zone.BuriedTurn.Remove(card);
                Log.Info($"伏击区域：玩家 {player.NetId} 回收埋牌 {card.Id.Entry} 到手牌");
                // ⚠️ **不能跳过视觉**：手牌必须有卡牌节点才能看见与打出，而 `skipVisuals: true`
                // 会让引擎**只把牌写进手牌数据、不创建节点**（CardPileCmd.cs 里 `!skipVisuals` 就是建节点的闸门）。
                // 2026-09-22 用户报障正是这个：牌其实在手里，但哪个堆都看不到、也打不出。
                // 区域牌本来就没有桌面节点，引擎找不到旧堆节点时会自己走"无飞行起点"的分支，不会炸。
                var added = await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom);
                if (!added.success)
                {
                    Log.Info($"伏击区域：回收埋牌 {card.Id.Entry} 未被手牌接受（success=false）");
                }
            }

            await NotifyCountChangedAsync(choiceContext, player, -count);
            return count;
        }

        /// <summary>
        /// 伏击区牌数发生变化时调用（「反复横跳」）：按变化量给玩家格挡。
        /// 埋入 / 生成埋入传 +1，带出传 -1，整片清空传 -张数；**战斗结束归还时不调用**（战斗已结束）。
        /// </summary>
        private static async Task NotifyCountChangedAsync(PlayerChoiceContext choiceContext, Player player, int delta)
        {
            if (delta == 0)
            {
                return;
            }

            SnowpackPower? power = player.Creature.GetPower<SnowpackPower>();
            if (power == null)
            {
                return;
            }

            // 2026-09-27 用户重做：只有**加入**（delta > 0）才结算——带出 / 消耗 / 清空不再送甲
            // （倾泄/收网一次清空不再白送一大笔格挡；带出侧的收益归被带出的牌本身）。
            if (delta < 0)
            {
                return;
            }

            await CreatureCmd.GainBlock(player.Creature, delta * power.Amount, ValueProp.Unpowered, null, fast: true);
        }

        /// <summary>
        /// 按区域当前内容重算增益、同步层数、刷新 UI。任何改动区域内容而不带走出流程的操作，
        /// 结算完自己的效果后都要调这个（详见 ExhaustAllAsync）。
        /// </summary>
        public static async Task RefreshZoneAsync(PlayerChoiceContext choiceContext, Player player)
        {
            await RecomputeBonusesAsync(choiceContext, player);
            await SyncPowerAsync(choiceContext, player);
            AmbushZoneUi.Refresh();
        }

        /// <summary>
        /// 战斗结束时归还该玩家所有未带出的埋牌（进弃牌堆）。伏击形态保留玩家手动切换的状态。
        /// </summary>
        public static async Task ReturnBuriedCardsAsync(Player player)
        {
            AmbushZoneState zone = For(player);
            while (zone.Pile.Cards.Count > 0)
            {
                CardModel card = zone.Pile.Cards[0];
                zone.Pile.RemoveInternal(card);
                zone.BuriedTurn.Remove(card);
                // Token 衍生牌（"设伏"生成的暗袭）不属于牌组，战斗结束直接销毁，不进弃牌堆
                if (card.Rarity == CardRarity.Token)
                {
                    Log.Info($"伏击区域：战斗结束，衍生牌 {card.Id.Entry} 直接销毁");
                    continue;
                }

                Log.Info($"伏击区域：战斗结束，{card.Id.Entry} 归入弃牌堆");
                await CardPileCmd.Add(card, PileType.Discard);
            }

            zone.BonusExtra = 0;
            zone.IsOverloaded = false;
            zone.IsCoolingDown = false;
            zone.BuriedTurn.Clear();
            await RecomputeBonusesAsync(new ThrowingPlayerChoiceContext(), player);
            await SyncPowerAsync(new ThrowingPlayerChoiceContext(), player);
            PlayingFromAmbush.Clear();
            VeteranAmbush.Clear();
            AmbushZoneUi.Refresh();
        }
    }
}
