using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 神速：**0 费金卡技能，自带[gold]消耗[/gold]**（2026-09-24 用户重做）。
    /// **[gold]消耗[/gold]你[gold]伏击区域[/gold]中的所有牌；若这次消耗了至少 `CardsRequired` 张
    /// （基础 **4**、升级 **3**），你获得**一个额外回合**。**
    /// 定位：伏击线的**终局兑现器**——把攒了一整轮的伏击区一次性烧成"这回合再来一次"。
    /// 门槛刻意卡在区域基础上限（3）之上：想吃到额外回合，必须先扩容（「张网以待」「扩张」、
    /// 遗物「暗杀者」），升级则把门槛降到 3、正好等于基础上限。自带[gold]消耗[/gold]，
    /// 一次对局只兑现一次（不会被循环复用）。
    /// <para>
    /// **2026-09-24 重做沿革**：本卡此前是**金卡能力**，效果是"打出时获得一个额外回合 +
    /// 之后每回合给自己挂 5 层冰冻（升级 3）"——即"用永久挂冰的债换一轮行动"。用户判定
    /// （原话）"神速的代价有些重"，且这张牌与伏击线零交互，于是**脱绑冰冻、改绑伏击**：
    /// 代价从"背一整局的冰债"变成"烧掉当前伏击区"，收益仍是额外回合，
    /// 与「倾泄」「重整计划」「收网」同属"区域兑现器"一族（本卡是其中唯一换节奏的）。
    /// 类型随之从 `CardType.Power` 改成 `CardType.Skill` 并加 `CardKeyword.Exhaust`——
    /// 一次性效果 + 耗尽自己的语义，比"永久挂在场上的能力牌"更贴。
    /// </para>
    /// <para>
    /// **额外回合的落地方式（不挂能力，直接由本卡承载）**：本体判定"是否给额外回合"走的是
    /// `Hook.ShouldTakeExtraTurn` → 遍历**所有战斗钩子监听者**（`CombatState.IterateHookListeners`
    /// 收录每个战斗牌堆里的牌，**含消耗牌堆**，见 PlayerCombatState.AllPiles）；
    /// 兑现走 `Hook.AfterTakingExtraTurn`，它会对**每一个**监听者调用（不是只调命中那个）。
    /// 所以本卡自己覆写这两个钩子即可：`_pendingTurns > 0` 才给、兑现时减 1（= 防无限额外回合的唯一闸门），
    /// 用完归零后即使牌还躺在消耗堆里也永远返回 false。
    /// 结构照本体 `MockExtraTurnPower`；判定写死 `player == Owner`，
    /// 所以**只有打出这张牌的人自己**再行动一个回合，队友不参与（本体引擎与 UI 都是按"个人额外回合"设计的）。
    /// ⚠️ 不要在 `ShouldTakeExtraTurn` 里读 `CombatManager.PlayersTakingExtraTurn`——本体在调用钩子
    /// **之前**就把它 `Clear()` 了，那里永远是空列表（`RampantPower` 能用是因为它在别的钩子里读）。
    /// </para>
    /// </summary>
    public sealed class Devour : CardModel, ILingdawnpaoCard
    {
        /// <summary>
        /// 尚未兑现的额外回合数（**不参与序列化，也不该参与**：它只在"打出这张牌"到"回合结束判定"
        /// 之间存活，战斗内即用即清）。每次达到门槛 +1、每兑现一个额外回合 -1；归零后永久失效。
        /// </summary>
        private int _pendingTurns;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        ];

        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("CardsRequired", 4m),
        ];

        public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

        public Devour()
            : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

            int consumed = await AmbushZone.ExhaustAllAsync(choiceContext, Owner);
            int required = (int)DynamicVars["CardsRequired"].BaseValue;
            if (consumed >= required)
            {
                _pendingTurns++;
                Log.Info($"神速：消耗 {consumed} 张（门槛 {required}）→ 登记一个额外回合");
            }
            else
            {
                Log.Info($"神速：只消耗 {consumed} 张（未达门槛 {required}）→ 无额外回合");
            }

            // 区域被清空 → 重算力 / 敏加成（照「收网」「重整计划」的收尾）
            await AmbushZone.RefreshZoneAsync(choiceContext, Owner);
        }

        /// <summary>照本体 MockExtraTurnPower：只有本人、且还有未兑现的额外回合时才给。</summary>
        public override bool ShouldTakeExtraTurn(Player player)
        {
            return player == Owner && _pendingTurns > 0;
        }

        /// <summary>照本体 MockExtraTurnPower：兑现一个（本体扣的是能力层数，我们扣的是私有计数）。</summary>
        public override Task AfterTakingExtraTurn(Player player)
        {
            if (player == Owner && _pendingTurns > 0)
            {
                _pendingTurns--;
            }

            return Task.CompletedTask;
        }

        protected override void OnUpgrade()
        {
            DynamicVars["CardsRequired"].UpgradeValueBy(-1m); // 门槛 4 → 3（更容易触发）
        }
    }
}