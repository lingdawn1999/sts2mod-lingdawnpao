using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 初始遗物「伏击者」：右键切换伏击形态（能力栏的「伏击区域」层数展示埋牌数量）。
    /// 伏击形态下出牌被拦截并埋入伏击区域，非伏击形态下每打出一张牌就带出一张（FIFO）。
    /// 所有会改变状态的操作都走同步动作队列（BuryCardAction / ToggleAmbushAction），
    /// 因此联机会话里每个客户端看到的伏击区域都一致。
    /// </summary>
    public class FrostFang : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Starter;

        // 双形态图标：初始=雪豹正脸，伏击=树丛中半藏半露的眼睛（贴图未就绪时自动回落）
        public const string ActiveAtlasPath = "res://images/atlases/relic_atlas.sprites/frost_fang_active.tres";
        public const string ActiveBigIconPath = "res://images/relics/frost_fang_active.png";

        public override string PackedIconPath =>
            Status == RelicStatus.Active && ResourceLoader.Exists(ActiveAtlasPath)
                ? ActiveAtlasPath
                : base.PackedIconPath;

        /// <summary>右键只负责请求切换，真正的翻转由 ToggleAmbushAction 在所有客户端上执行。</summary>
        public void ToggleAmbushMode()
        {
            Player? owner = Owner;
            if (owner == null || !LocalContext.IsMe(owner))
            {
                return;
            }

            ActionQueueSynchronizer? synchronizer = RunManager.Instance.ActionQueueSynchronizer;
            if (synchronizer == null)
            {
                Log.Info("伏击者：不在局内，忽略切换伏击形态");
                return;
            }

            bool enabled = !AmbushZone.For(owner).IsAmbushMode;
            Log.Info($"伏击者：右键请求切换伏击形态 → {(enabled ? "开启（收集）" : "关闭（倾泻）")}");
            synchronizer.RequestEnqueue(new ToggleAmbushAction(owner, enabled));
        }

        /// <summary>切换动作在各客户端执行时回调：同步遗物外观与提示特效。</summary>
        public void OnAmbushModeSynced()
        {
            Player? owner = Owner;
            if (owner == null)
            {
                return;
            }

            bool enabled = AmbushZone.For(owner).IsAmbushMode;
            Status = enabled ? RelicStatus.Active : RelicStatus.Normal;
            Log.Info($"伏击者：伏击形态已切换 → {(enabled ? "开启（收集）" : "关闭（倾泻）")}");
            Flash();
            if (owner.PlayerCombatState != null)
            {
                // 角色身上一圈遗物光效 + 能力图标闪烁，作为切换提示
                Flash([owner.Creature]);
                owner.Creature.GetPower<AmbushZonePower>()?.Pulse();

                // 手牌里那张姿态牌本身也要换卡名 / 描述 / 卡图（它是当前形态的说明书，见 AmbushStance）
                AmbushStance.RefreshFormCardVisuals(owner);

                // 整手费用由 ToggleAmbushAction 在遗物视觉回调之前统一刷新。
            }
        }

        public override async Task BeforeCombatStart()
        {
            Player? owner = Owner;
            if (owner != null) PendingBuryVisual.Clear(owner);
            AmbushZoneState? zone = owner == null ? null : AmbushZone.For(owner);
            // **进战斗的兜底重置**：冷却是回合级别的状态，正常由 ReturnBuriedCardsAsync
            // （AfterCombatEnd）清零。但战斗结束流程并不保证一定跑到那里——本体结算中途抛异常
            // （2026-09-23 那个 epoch 崩溃就会打断 EndCombatInternal）、玩家放弃对局等都会漏掉。
            // 一旦漏掉，普通冷却会带入下一场战斗，但负责恢复的状态已经被清掉，导致整场不能埋牌，
            // 而且界面上看不出任何冷却。用户 2026-09-23 报障"伏击区没进冷却但不能埋牌"优先怀疑这里。
            if (zone != null)
            {
                if (zone.IsCoolingDown)
                {
                    Log.Info("伏击者：进战斗时发现残留冷却，已清除");
                    zone.IsCoolingDown = false;
                }

                // 「十面埋伏」的伤害成长按"本场战斗"计，进战斗必须归零（重复进战斗时同理）
                zone.Effects.Clear();
                zone.PowerCapacityUsedThisTurn = false;
            }

            // 挂上伏击区上限能力（用户 2026-09-23 要求"开局就有 buff 提示伏击区上限"）：
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

            Log.Info($"伏击者：进入战斗（伏击形态={zone?.IsAmbushMode}，区域牌数={zone?.Count}）");
        }

        /// <summary>
        /// 每场战斗的**第一次抽牌前**（以及此后每次回合开始）确保手牌区里有一张「伏击姿态」
        /// ——玩家改从手牌区右键切换伏击形态（遗物右键保留作兜底）。
        /// <para>
        /// 为什么挂在这里而不是 <see cref="BeforeCombatStart" />：这个钩子正好在初始抽 5 张**之前**
        /// （`CombatManager.StartTurn` 里 `BeforeHandDraw` → 算抽牌数 → `CardPileCmd.Draw`），
        /// 所以既做到"每场战斗开局就有"，又**不影响初始抽牌数**；而且此时手牌 UI 已经建好，
        /// 加牌不会碰到 `NCombatRoom.Instance.Ui.Hand` 还没就绪的空窗。
        /// 顺带成了"补回"机制：万一它被什么效果弄出牌堆，下一次抽牌前会自动回来。
        /// </para>
        /// </summary>
        public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
        {
            // 联机：本钩子对所有玩家各跑一次（`Toolbox` 等本体遗物也按 owner 过滤），
            // 不过滤就会给队友的手牌区也塞一张。
            if (player != Owner)
            {
                return;
            }

            // 新回合重置「首次使用能力牌」标记（2026-09-27 上限新规则，见 AfterCardPlayed）
            AmbushZone.For(player).PowerCapacityUsedThisTurn = false;

            await AmbushStance.EnsureInHandAsync(player);
            LingdawnpaoAnimationController.SyncMode(player);
        }

        /// <summary>
        /// 首回合启动存货（2026-10-28 用户定稿）：**每场战斗的第一回合开始时**，可自选 1 张手牌
        /// **免费**加入[gold]伏击区域[/gold]（可选、能跳过）——治"开局真空期"：
        /// 区域空 1~2 回合是伏击流最弱的窗口，这个保底让第一回合末就有存货可带出。
        /// <para>
        /// 钩子与「暗杀者」同源（AfterPlayerTurnStart：摸牌之后，能看到刚摸的牌）。
        /// **暗杀者不叠加本效果**：它 override 了本钩子做"每回合自选"，第一回合已被覆盖
        /// （C# 虚方法覆盖后本实现不会跑，天然不重复弹窗）。
        /// </para>
        /// </summary>
        public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        {
            if (player != Owner || player.PlayerCombatState == null || this is Assassin)
            {
                return;
            }

            if (player.PlayerCombatState.TurnNumber != 1)
            {
                return;
            }

            AmbushZoneState zone = AmbushZone.For(player);
            if (zone.IsCoolingDown || zone.IsFull || PileType.Hand.GetPile(player).Cards.Count == 0)
            {
                return;
            }

            List<CardModel> picked = (await CardSelectCmd.FromHand(
                choiceContext,
                player,
                new CardSelectorPrefs(SelectionScreenPrompt, 0, 1),
                CanBuryForFree,
                this)).ToList();
            if (picked.Count == 0 || picked[0].Pile?.Type != PileType.Hand)
            {
                return;
            }

            Flash();
            Log.Info($"伏击者：首回合自选埋牌 {picked[0].Id.Entry}（玩家 {player.NetId}）");
            await AmbushZone.BuryAsync(choiceContext, picked[0], player, spendResources: false);
        }

        /// <summary>
        /// 免费埋牌的可选范围：与手动埋牌保持一致（BuryCardAction 要求 CanPlay），
        /// 但因为是免费埋入，"只因能量/辉星不足而打不出"的牌也允许选（暗杀者共用）。
        /// </summary>
        internal static bool CanBuryForFree(CardModel card)
        {
            if (card.CanPlay(out UnplayableReason reason, out AbstractModel? _))
            {
                return true;
            }

            return reason.HasFlag(UnplayableReason.EnergyCostTooHigh)
                || reason.HasFlag(UnplayableReason.StarCostTooHigh);
        }

        // 能力牌埋入收益改为"埋入瞬间给下回合摸牌"（见 AmbushZone.BuryAsync），
        // 不再按区域内能力牌数量持续加摸牌，避免囤牌数值膨胀。

        // 非伏击形态：每张手动打出的牌结算后，带出最早的伏击牌（FIFO）。
        // 带出的牌经 AutoPlay 打出，其 CardPlay.IsAutoPlay 为 true，不会再次触发（防连锁）。
        // 豁免名单分两类：
        //
        // 【一】自己就要操作伏击区的牌，遗物别插手：
        // ①「猛虎连击」在自己的 OnPlay 里逐张带出驱动连击，遗物不能再补带一张；
        // ②「重整计划」「雪灾」自己要清算伏击区换资源——遗物若在它们 OnPlay 之后带出一张，
        //    就把"清空"变成"清 N-1 张 + 带出 1 张"，与卡牌意图相悖
        //    （2026-09-23 用户报障："先带出一张再开始烧"）。雪灾还多一层：它先清空、再用「晕眩」填满，
        //    遗物立刻带出一张晕眩（不可打出，直接进弃牌堆）会让"填满"变成只填 2 张。
        //    「重整计划」2026-10-24 起改为**自己带出打出全区域**（每张改消耗 + 回资源），
        //    它自己就是带出器，遗物若再补带一张等于白送一次带出，豁免理由更成立。
        //
        // 【二】**生成件**——打出时往区域里塞新牌，顺序必须是"先带出旧牌、再生成"：
        // ③「设伏」「捕猎」「永恒呼吸」「扑击」「珍藏」「雪球技巧」。遗物挂在出牌**之后**，那时新牌已经躺在区域里，
        //    区域原本为空时它带出的就是这张卡自己刚塞进去的牌（用户 2026-09-22 报障"这个尽量别带"）。
        //    现改由卡内先带出（`AmbushZone.BringOutBeforeGenerateAsync`），遗物这边整张豁免。
        //    ⚠️ **不要退回"只豁免、不带出"**：那会让这类牌连区域里的旧牌也带不出来
        //    （2026-09-23 用户报障"修改设伏之后，设伏不能正常带出普通卡了"）。
        //    「珍藏」是 2026-09-24 加进来的成员：它检索到带[gold]埋伏[/gold]词条的牌会**直接埋入区域**，
        //    遗物不豁免的话"刚塞进去的一张就被立刻带出来"，直接入区的意义当场作废。
        //    「雪球技巧」同日加进来：它会当场用「雪球」填满区域，同样必须豁免 + 卡内自带出旧牌。
        //    「冰镜」只复制区域牌到手牌，原牌与区域顺序保持不变，不额外带出。
        //    「重整计划」定向带出并补入手牌；「凝神」批量放入手牌，两者均不额外触发 FIFO。
        //    「厚绒」手动打出时先结算旧牌带出，再将自己加入区域，避免空区时把自己立刻带出。
        //    （「雪拥」曾短暂进过这张名单，随它回退成纯通用格挡件而一并移除——别再加回来。）
        public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            Player? owner = Owner;
            if (owner == null)
            {
                return;
            }

            AmbushZoneState zone = AmbushZone.For(owner);

            // **每回合首次使用能力牌 → 上限+1**（2026-09-27 新规则）：
            // 伏击形态下埋掉的能力牌不走出牌流程、不进本钩子，自然不算"使用"；
            // **带出打出的能力牌也算"使用"**（2026-09-28 用户报障修复：原先 IsAutoPlay 在钩子
            // 开头就早退，导致从伏击区带出的能力牌永远触发不了扩容——带出走 AutoPlay，
            // IsAutoPlay 恒为 true，故这个判断必须放在 IsAutoPlay 闸门**之前**）。
            // 自动打出来源不限（遗物/其它卡的效果自动打出的能力牌同样算），反正每回合只有首次生效。
            // 扩容走统一入口（硬顶 9，见 AmbushZone.AddCapacityAsync）。
            if (cardPlay.Card.Type == CardType.Power && cardPlay.Card.Owner == owner
                && !zone.PowerCapacityUsedThisTurn)
            {
                zone.PowerCapacityUsedThisTurn = true;
                await AmbushZone.AddCapacityAsync(choiceContext, owner, 1, "本回合首次使用能力牌");
                Flash();
            }

            // 带出打出的牌（IsAutoPlay）不再触发"非伏击形态带出"，防连锁——扩容判断在上面已完成
            if (cardPlay.IsAutoPlay)
            {
                return;
            }

            // ── 以下为"非伏击形态带出"逻辑，豁免名单与口径见下方原注释 ──
            if (HandlesOwnBringOut(cardPlay.Card))
            {
                return;
            }

            // 联机：本钩子会收到所有玩家的出牌（本体 28 个用它的遗物里 26 个都做了 owner 过滤），
            // 不过滤的话队友出牌会把我们自己的埋牌带出来，与"你打出一张牌"的本意不符。
            if (cardPlay.Card.Owner != owner)
            {
                return;
            }

            if (zone.IsAmbushMode || zone.Count == 0)
            {
                return;
            }

            Log.Info($"伏击者：非伏击形态，带出最早的伏击牌（剩余 {zone.Count}）");
            await AmbushZone.BringOutFirstAsync(choiceContext, cardPlay.Target, owner);
        }

        internal static bool HandlesOwnBringOut(CardModel card) => card is
            TigerCombo or SetAmbush or Hunt or EternalBreath or BodyPounce or SnowCalamity or Regroup or Trove
            or SnowballTechnique or Stockpile or SnowfieldDash or SnowSift or IceMirror or Concentration or ThickCoat
            or LingdawnpaoInitiative or CloseTheNet;

        // 战斗结束：未带出的埋牌归入弃牌堆，区域清空；伏击形态保持玩家手动切换的状态
        public override async Task AfterCombatEnd(CombatRoom room)
        {
            Player? owner = Owner;
            if (owner == null)
            {
                return;
            }

            PendingBuryVisual.Clear(owner);
            AmbushZoneState zone = AmbushZone.For(owner);
            Status = zone.IsAmbushMode ? RelicStatus.Active : RelicStatus.Normal;
            zone.Effects.Clear();   // 「十面埋伏」的成长是"本场战斗"口径，战后清账
            await AmbushZone.ReturnBuriedCardsAsync(owner);
            Log.Info($"伏击者：战斗结束，区域已清空（伏击形态仍为 {zone.IsAmbushMode}）");
        }
    }
}
