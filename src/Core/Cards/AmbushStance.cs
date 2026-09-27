using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 「伏击姿态」（2026-09-24 用户要求）：把"切换伏击形态"从**遗物图标**搬进**手牌区**。
    /// 起因：遗物 UI 离卡牌区太远，玩家"切形态 → 出牌"要来回移动鼠标，操作极其别扭。
    /// 局内手牌区的右键**原本什么也不做**（不是"查看"），所以正好拿来当快捷键。
    /// 遗物右键**保留**作兜底（不进教学提示）。
    /// <para>
    /// **一卡两形态**（用户 2026-09-24 追加）：这张牌本身就是当前形态的说明书，
    /// 卡名 / 卡图 / 描述都随伏击形态切换：
    /// · 常规形态（默认）：卡名「常规形态」，描述"出牌时将会带出最先加入伏击区域的一张牌"；
    /// · 伏击形态：卡名「伏击形态」，描述"打出牌时不触发效果，并加入伏击区域"。
    /// 卡图用**先古格式**（<see cref="CardRarity.Ancient" /> 的竖构图 606×852，与普通卡的 1000×760 不同）。
    /// </para>
    /// <para>
    /// 规则（用户逐条指定，见各自实现）：
    /// ① 每场战斗开始时自动进入手牌（<see cref="FrostFang.BeforeHandDraw" />），不进卡组、不入牌组循环；
    /// ② 不能被**打出**（<see cref="CardKeyword.Unplayable" />）、不能被**升级**（<see cref="MaxUpgradeLevel" />）；
    /// ③ 不能被**消耗/弃置/变形**、也不能被移出手牌（三个补丁 + 选牌过滤）；
    /// ④ 回合结束**留在手里**（<see cref="CardKeyword.Retain" />）；
    /// ⑤ 不占抽牌位、不影响开局抽牌数（在抽牌**之前**入手的，只当它的存在不影响任何一张牌的抽取）；
    /// ⑥ **不占手牌上限**（<see cref="HandLimit" /> 与三个转译补丁）；
    /// ⑦ 不能被以其他形式获取（<see cref="CanBeGeneratedInCombat" /> / <see cref="CanBeGeneratedByModifiers" />，
    ///    且只登记进 TokenCardPool —— 奖励/商店只从角色池取卡）。
    /// </para>
    /// <para>
    /// ⚠️ **卡面不显示关键词行**（用户明确要求："经过特殊处理了，不要再加保留/固有一类词条，徒增阅读成本"）：
    /// `Unplayable` / `Retain` 只作为**行为底座**留在 <see cref="CanonicalKeywords" /> 里
    /// （`CanPlay` 与 `FlushPlayerHand` 认的就是它们，去掉会引发不可预期的副作用），
    /// 而卡面文案走 <see cref="FormDescription" />，由
    /// <see cref="CardModel_GetDescriptionForPile_AmbushStanceFormPatch" /> 整段替换掉
    /// 本体"描述 + 关键词行"的拼装结果。
    /// </para>
    /// </summary>
    public sealed class AmbushStance : CardModel, ILingdawnpaoCard
    {
        /// <summary>
        /// 当前是否处于伏击形态（决定卡名/卡图/描述用哪一套）。
        /// 只认持有者自己的伏击状态；卡牌百科里的本体实例没有 Owner，一律按常规形态显示。
        /// </summary>
        public bool IsInAmbushForm =>
            Owner is { PlayerCombatState: not null } player && AmbushZone.For(player).IsAmbushMode;

        /// <summary>
        /// 卡图钉死在雪豹目录（同 <see cref="AmbushStrike" /> 的说明），**按形态换图**。
        /// <para>
        /// ⚠️ **先古格式 = 竖构图 606×852**（本体 ver. 的先古卡 `corruption.png` / `apotheosis.png`
        /// 就是这个尺寸，普通卡是横向 1000×760）。图缺失时本体直接给 null 贴图、不会崩，
        /// 出图后放这两个文件即可：
        /// `images/packed/card_portraits/lingdawnpao/ambush_stance.png`（常规形态）
        /// `images/packed/card_portraits/lingdawnpao/ambush_stance_ambush.png`（伏击形态）。
        /// </para>
        /// </summary>
        public override string PortraitPath =>
            "res://images/atlases/card_atlas.sprites/lingdawnpao/"
            + (IsInAmbushForm ? "ambush_stance_ambush" : "ambush_stance") + ".tres";

        /// <summary>卡名按形态切换（卡面 / 卡牌百科 / 各处标题都用这里）。</summary>
        public override string Title => FormText(".ambushTitle", ".title");

        /// <summary>
        /// 卡面描述（**整段替换**本体拼装结果，见
        /// <see cref="CardModel_GetDescriptionForPile_AmbushStanceFormPatch" />）。
        /// </summary>
        public string FormDescription => FormText(".ambushDescription", ".description");

        /// <summary>不能被升级（本体所有升级入口都先查 `IsUpgradable`，而它读的就是这里）。</summary>
        public override int MaxUpgradeLevel => 0;

        /// <summary>不能被随机生成类效果摸到（"不能被以其他形式获取"）。</summary>
        public override bool CanBeGeneratedInCombat => false;

        /// <summary>不能被改造类效果生成（同上）。</summary>
        public override bool CanBeGeneratedByModifiers => false;

        // Unplayable：不能被打出（本体 Dazed/Injury 同款，走动作用域在 `CanPlay`）。
        // Retain：回合结束时留在手里（`CombatManager.FlushPlayerHand` 只认 `ShouldRetainThisTurn`）。
        // ⚠️ 这两个词条**只做行为、不上卡面**：卡面文案见 FormDescription（用户要求不要词条行）。
        public override IEnumerable<CardKeyword> CanonicalKeywords =>
            new[] { CardKeyword.Unplayable, CardKeyword.Retain };

        public AmbushStance()
            // 先古稀有度 = 用户指定的"先古卡图格式"（NCard 会换成先古边框/立绘槽/文字底）。
            // 费用 -1：本体 `_energyIcon.Visible = cost >= 0`，所以不显示费用（同 Dazed）。
            : base(-1, CardType.Status, CardRarity.Ancient, TargetType.None)
        {
        }

        /// <summary>
        /// 取当前形态对应的本地化文本；伏击形态的 key 万一缺失就退回常规形态的，
        /// 免得卡面渲染（NCard.UpdateVisuals）抛异常把整个手牌 UI 带崩。
        /// </summary>
        private string FormText(string ambushSuffix, string normalSuffix)
        {
            LocString loc = new LocString("cards", base.Id.Entry + (IsInAmbushForm ? ambushSuffix : normalSuffix));
            if (IsInAmbushForm && !loc.Exists())
            {
                loc = new LocString("cards", base.Id.Entry + normalSuffix);
            }

            return loc.GetFormattedText();
        }

        private static readonly MethodInfo? ReloadMethod = AccessTools.Method(typeof(NCard), "Reload");

        /// <summary>
        /// 形态切换后刷新手牌里那张牌的卡面（卡名 / 描述 / 卡图 / 边框）。由
        /// <see cref="FrostFang.OnAmbushModeSynced" /> 在切换动作执行完时调。
        /// <para>
        /// ⚠️ 光调 <c>NCard.UpdateVisuals</c> **不够**：卡名与描述会更新，但**卡图不会**——
        /// 立绘/边框是在 `NCard.Reload()` 里取的，而本体的 `Model` / `Visibility` setter
        /// 只在值**变化**时才调它（这里换的是形态、模型对象没变）。所以反射补一次 `Reload()`。
        /// </para>
        /// <para>只影响本地手牌：队友的手牌在本地没有节点，`FindOnTable` 返回 null 直接跳过。</para>
        /// </summary>
        public static void RefreshFormCardVisuals(Player player)
        {
            CardPile? hand = player.PlayerCombatState?.Hand;
            if (hand == null)
            {
                return;
            }

            foreach (CardModel card in hand.Cards)
            {
                if (card is not AmbushStance stance)
                {
                    continue;
                }

                NCard? node = NCard.FindOnTable(stance, PileType.Hand);
                if (node == null)
                {
                    continue;
                }

                ApplyFormVisualsToNode(node, stance);
            }
        }

        /// <summary>Reload + 刷卡面 + 换形态立绘，三件套（由 RefreshFormCardVisuals 与 UpdateVisuals 补丁共用）。</summary>
        private static void ApplyFormVisualsToNode(NCard node, AmbushStance stance)
        {
            ReloadMethod?.Invoke(node, null);
            node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            ApplyFormPortrait(node, stance);
            Log.Info($"伏击姿态：卡面已按「{(stance.IsInAmbushForm ? "伏击形态" : "常规形态")}」刷新", 2);
        }

        /// <summary>
        /// 形态立绘切换：伏击形态换 `ambush_stance_ambush.png`，常规换 `ambush_stance.png`；
        /// 图缺失时回落 PortraitPath。先古立绘槽（%AncientPortrait）与普通立绘槽（%Portrait）都要换。
        /// 由 <see cref="NCard_UpdateVisuals_AmbushStanceFormPatch" /> 在每次卡面刷新时调用。
        /// </summary>
        public static void ApplyFormPortrait(NCard node, AmbushStance stance)
        {
            string path = "res://images/packed/card_portraits/lingdawnpao/"
                + (stance.IsInAmbushForm ? "ambush_stance_ambush" : "ambush_stance") + ".png";
            Texture2D? formPortrait = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse)
                ?? ResourceLoader.Load<Texture2D>(stance.PortraitPath, null, ResourceLoader.CacheMode.Reuse);
            if (formPortrait == null)
            {
                return;
            }

            TextureRect? ancientSlot = node.GetNodeOrNull<TextureRect>(new NodePath("%AncientPortrait"));
            TextureRect? normalSlot = node.GetNodeOrNull<TextureRect>(new NodePath("%Portrait"));
            if (ancientSlot != null)
            {
                ancientSlot.Texture = formPortrait;
            }

            if (normalSlot != null)
            {
                normalSlot.Texture = formPortrait;
            }
        }

        /// <summary>
        /// 刷新**整个手牌**的卡面（含费用数字）。由 <see cref="FrostFang.OnAmbushModeSynced" /> 在切换伏击形态时调。
        /// <para>
        /// 起因（2026-09-25 用户报障）：「永恒狩猎」的免费埋牌是靠
        /// `EternalHuntPower.TryModifyEnergyCostInCombatLate` 把耗能改成 0 的，而这个值只在
        /// **卡面刷新**时才重新取（`NCard.UpdateVisuals` → `UpdateEnergyCostVisuals` →
        /// `CardEnergyCost.GetWithModifiers(All)` → `Hook.ModifyEnergyCostInCombat`）。
        /// 切形态本身不会触发手牌刷新，于是"切到伏击形态（免费窗口打开）"之后卡面仍显示原费用，
        /// 玩家以为还要付这笔钱——刷一遍就能让纸面费用立刻变 0。
        /// </para>
        /// <para>
        /// 用 `UpdateVisuals` 就够，**不需要** `Reload()`（那是给卡图/边框用的，见
        /// <see cref="RefreshFormCardVisuals" />）；费用数字是 `UpdateVisuals` 的一部分。
        /// 只影响本地手牌：队友的手牌在本地没有节点，`FindOnTable` 返回 null 直接跳过。
        /// </para>
        /// </summary>
        public static void RefreshHandVisuals(Player player)
        {
            CardPile? hand = player.PlayerCombatState?.Hand;
            if (hand == null)
            {
                return;
            }

            foreach (CardModel card in hand.Cards)
            {
                NCard? node = NCard.FindOnTable(card, PileType.Hand);
                node?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            }
        }

        /// <summary>
        /// 手牌上限（**转译补丁的替换目标**）：姿态牌在手时宽限 1 格。
        /// <para>
        /// ⚠️ **不能靠补丁 `CardPile.MaxCardsInHand` 本体**：那是个只有两行 IL 的常量属性，
        /// 会被 CoreCLR JIT 内联进调用方，detour 直接被绕过（本项目 2026-09 在
        /// `CardModel.TryManualPlay` 上实测过这个坑）。**转译器改的是调用方自己的 IL**，
        /// 无论调用方怎么内联都绕不过去 —— 所以这里必须逐个调用点转译，
        /// 见 <see cref="HandLimitTranspiler" />。
        /// </para>
        /// <para>
        /// ⚠️ 判据刻意是"**任意**持有者手牌里有姿态牌"而不是"本地玩家有"：
        /// `MaxCardsInHand` 是无参静态属性，调用点根本拿不到"这是谁的手牌"。
        /// 若按本地玩家判定，联机两端会各算各的（一端宽限、一端不宽限）→ 抽牌张数脱同步。
        /// 按"本场战斗有没有人持有姿态牌"判定则两端完全一致。
        /// 代价：混编联机里非雪豹角色也会多 1 格 —— 取舍是不脱同步优先。
        /// </para>
        /// </summary>
        public static int HandLimit =>
            CardPile.MaxCardsInHand + (AmbushZone.AnyHandHoldsFormCard() ? 1 : 0);

        /// <summary>
        /// 选牌界面里**不可被选中**（2026-09-24 用户追加要求："不要让它成为其他牌的耗材"）。
        /// <para>
        /// 「消耗你的一张手牌」「弃一张手牌」「变形一张手牌」「把一张手牌放回抽牌堆」这类效果的流程是：
        /// 先 `CardSelectCmd.FromHand` 弹出选牌界面让玩家挑 → 挑完才执行。本体**没有任何统一过滤**
        /// （`FromHand` 的默认 filter 是 `_ =&gt; true`，只有少数几张卡自己排除了 Unplayable），
        /// 所以不做这一步的话，玩家能把它当耗材选中——选中后又被
        /// <see cref="CardPileCmd_Add_AmbushStanceGuardPatch" /> 静默拦下，效果就"点了没反应"。
        /// 在选牌阶段排除掉，玩家根本看不到它（`NPlayerHand.UpdateSelectModeCardVisibility`
        /// 就是按 filter 的返回值隐藏卡片的）。
        /// </para>
        /// </summary>
        public static bool IsSelectable(CardModel card) => card is not AmbushStance;

        /// <summary>
        /// 确保该玩家手牌区里躺着一张姿态牌（已在手牌则什么也不做）。
        /// 由 <see cref="FrostFang.BeforeHandDraw" /> 在**每次抽牌前**调用，
        /// 因此每场战斗开局都有，且万一被什么奇技淫巧弄出牌堆也会自然补回。
        /// </summary>
        public static async Task EnsureInHandAsync(Player player)
        {
            PlayerCombatState? combat = player.PlayerCombatState;
            if (combat == null || !CombatManager.Instance.IsInProgress)
            {
                return;
            }

            CardPile hand = combat.Hand;
            if (hand.Cards.Any(card => card is AmbushStance))
            {
                return;
            }

            // 不在手牌里但躺在别的战斗牌堆（例：某药水把整只手牌塞进抽牌堆）→ 搬回来，**不要再造一张**，
            // 否则会越攒越多。
            foreach (CardPile pile in combat.AllPiles)
            {
                if (pile.Type == PileType.Hand)
                {
                    continue;
                }

                CardModel? stray = pile.Cards.FirstOrDefault(card => card is AmbushStance);
                if (stray != null)
                {
                    Log.Info($"伏击姿态：发现它躺在 {pile.Type} 牌堆里，移回手牌");
                    await CardPileCmd.Add(stray, hand);
                    return;
                }
            }

            ICombatState? state = player.Creature.CombatState;
            if (state == null)
            {
                return;
            }

            AmbushStance stance = state.CreateCard<AmbushStance>(player);
            await CardPileCmd.AddGeneratedCardToCombat(stance, PileType.Hand, player);
            Log.Info("伏击姿态：已放入手牌（本场战斗首次）");
        }
    }
}