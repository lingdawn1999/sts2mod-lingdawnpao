using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
    /// 冰封王座（玩家增益，由「冰封王座」蓝卡赋予）：**每回合第一次**能量不足以支付某张牌时，
    /// 该牌耗能为 0、仍可正常打出；以此法打出的牌，**每差 1 点能量**使你自己获得 Amount 层「冰冻」
    /// （Amount = 3，升级后的「冰封王座」写入 2 —— 由卡在打出时 `SetAmount` 覆盖写入，见 `FrostThrone.OnPlay`）。
    /// 冰冻在这张卡里是**代价**，层数降低等于**减轻自伤**，所以"升级到 2"是加强方向。
    /// <para>
    /// **2026-09-24 用户两改**：① 加「**每回合第一次**」阀门（原为当回合内无限次）；
    /// ② 计费口径由"每 1 点**原始耗能**"改为"每**差** 1 点能量"（差额 = 原始耗能 − 当前能量），
    /// 数值 4/3 → **3/2**。改因是「神速」脱绑冰冻改绑伏击后，冰冻线的自伤来源少了一条，
    /// 用户判定冰封王座的代价偏重（"冰封王座可以限降低代价"）。
    /// 差额口径顺手把"只差 1 费"的高费牌变得便宜——本就该按欠多少算，不再按牌面价算。
    /// </para>
    /// 挂到自己身上的冰冻走的是同一套结算（获得时立即受到等量伤害，**可被格挡**），所以这是一张
    /// **拿命换节奏**的牌：不但自伤，而且玩家也受"生命值低于冰冻层数即死"的处决线约束
    /// （用户 2026-09-20 明确要求保留处决）。玩家身上的 DoomKill 已核实安全——
    /// `DoomPower.PlayVfx` 对非怪物走 `shouldDie=false` 分支（不删节点、不播死亡动画），随后正常 Kill。
    ///
    /// 实现：照本体「虚空形态」VoidFormPower 用 `TryModifyEnergyCostInCombatLate` 把耗能改成 0
    /// （这样 `UnplayableReason.EnergyCostTooHigh` 的判定会自动放行），同时在钩子里记下**差额**，
    /// 等 `AfterCardPlayed` 再按 `差额 × Amount` 结算冰冻。
    /// ⚠️ **额度只在真花掉时才消费**（`SettleDeferredAsync` 里置 `_loanUsedThisTurn`）——
    /// 耗能钩子会被 UI 反复调用做预览，在钩子里置位会让"看一眼"就白吃掉这一回合的额度。
    /// 三个排除项：① 联机下本钩子会被所有玩家调用 → 按 owner 过滤；② 从伏击区域带出的牌
    /// （AutoPlay / IsAmbushPlay）本来就免费（埋进去时已付费），不参与也不给冰冻；③ 差 0 点的牌不记账。
    /// <para>
    /// ⚠️ **埋牌也要结算**：埋牌是**付费**的（`AmbushZone.BuryAsync` 里的 `card.SpendResources()`），
    /// 但它**不走 `AfterCardPlayed`**（牌没被打出，而是被塞进伏击区域）——于是"能量不足 → 这里把耗能
    /// 改成 0 → 玩家照样埋进去"会变成纯白嫖（费用免了、冰冻挂不上）。所以 `BuryAsync` 扣完费必须
    /// 显式调 <see cref="SettleBuriedCostAsync" /> 补上自伤（2026-09-23 用户报障）。
    /// </para>
    /// 自伤刻意**不吃自己的「冰封」**（applier 传 null）：「冰封」的文案是"冰冻造成的伤害额外触发"，
    /// 那是打敌人的放大器；若自伤也被放大，冰封流会被自己的引擎反噬，属于未指定的反直觉联动。
    /// </summary>
    public sealed class FrostThronePower : PowerModel
    {
        /// <summary>「查询耗能 → 真正打出」这段窗口内记下被免费掉的**差额**（CardModel 无自定义 Equals，引用相等即可）。</summary>
        private readonly Dictionary<CardModel, decimal> _deferredCosts = new();

        /// <summary>
        /// **本回合是否已经用过那次"贷款"**（2026-09-24 用户加的阀门：每回合只有第一次能量不足时能白打）。
        /// 只在贷款**真的被消费**时置位（`SettleDeferredAsync`）——不能在耗能钩子里置位：
        /// 那个钩子会被 UI 反复调用做预览，置在那儿会被"看一眼"白白吃掉额度。
        /// </summary>
        private bool _loanUsedThisTurn;

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        /// <summary>每回合清掉上一回合残留的未消费记录与贷款额度。</summary>
        public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
        {
            if (participants.Contains(base.Owner))
            {
                _deferredCosts.Clear();
                _loanUsedThisTurn = false;
            }

            return Task.CompletedTask;
        }

        public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
        {
            modifiedCost = originalCost;
            if (!TryGetShortfall(card, originalCost, out decimal shortfall))
            {
                return false;
            }

            _deferredCosts[card] = shortfall;
            modifiedCost = default(decimal);
            return true;
        }

        public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (cardPlay.IsAutoPlay || cardPlay.Card.Owner.Creature != base.Owner)
            {
                return;
            }

            // `Resources.EnergyValue` = 打出这张牌**实际花掉的能量**（`PlayCardAction` 里 = `SpendResources()`
            // 的返回值，被 hook 减到 0 时就是 0；本体「野性」FeralPower 判"0 费攻击"用的是同一个字段）。
            // 本钩子（耗能查询）会被 UI 反复调用，所以字典里可能留着"当时能量不足"的残影——只有这次真的是
            // 0 费打出（贷款生效）才结算，否则说明是"付得起、按原价打出"，那条必须丢掉，不能凭空扎自己一脸冰冻。
            if (cardPlay.Resources.EnergyValue != 0)
            {
                _deferredCosts.Remove(cardPlay.Card);
                return;
            }

            await SettleDeferredAsync(choiceContext, cardPlay.Card, "免费打出");
        }

        /// <summary>
        /// **埋牌路径的结算入口**（2026-09-23 用户报障"贷款埋牌没有任何代价、没被挂上冰冻"）。
        /// <para>
        /// 埋牌不走 `AfterCardPlayed`（牌根本没被打出，而是被 `BuryCardAction` 塞进伏击区域），
        /// 但埋牌**是要付费的**（`AmbushZone.BuryAsync` 里的 `card.SpendResources()`）——于是
        /// "能量不足 → 冰封王座把耗能改成 0 → 玩家照样埋进去"就成了纯白嫖：费用被免了、冰冻代价
        /// 却挂不上，`_deferredCosts` 里那条记录要等回合开始才被清掉。`BuryAsync` 扣完费必须来敲
        /// 这个门把自伤补上。调用点只在"真的 0 费埋进去"时调（见 `AmbushZone.BuryAsync` 的判定），
        /// 所以这里不再判 owner：拿不到记录（不是冰封王座记的）就直接返回。
        /// </para>
        /// </summary>
        public async Task SettleBuriedCostAsync(PlayerChoiceContext choiceContext, CardModel card) =>
            await SettleDeferredAsync(choiceContext, card, "贷款埋牌");

        private async Task SettleDeferredAsync(PlayerChoiceContext choiceContext, CardModel card, string reason)
        {
            if (!_deferredCosts.Remove(card, out decimal shortfall) || shortfall <= 0m)
            {
                return;
            }

            // 贷款真的花掉了 → 本回合的额度用尽（下次耗能钩子不再放行）
            _loanUsedThisTurn = true;
            int stacks = (int)shortfall * Amount;
            Log.Info($"冰封王座：{reason} {card.Id.Entry}（本次差额 {shortfall} 点能量）→ 自身获得 {stacks} 层冰冻");
            await FrostbitePower.ApplyFrostAsync(choiceContext, base.Owner, stacks, null, card);
        }

        /// <summary>
        /// 是否给这张牌放贷；是则给出**差额**（`原始耗能 - 当前能量`，文案里的"每差 1 点能量"就是这个）。
        /// </summary>
        private bool TryGetShortfall(CardModel card, decimal cost, out decimal shortfall)
        {
            shortfall = 0m;

            // 每回合只有第一次（额度在 SettleDeferredAsync 里消耗）
            if (_loanUsedThisTurn || card.Owner.Creature != base.Owner || cost <= 0m)
            {
                return false;
            }

            PileType? pile = card.Pile?.Type;
            if (pile != PileType.Hand && pile != PileType.Play)
            {
                return false;
            }

            // 从伏击区域带出的牌本来就免费（埋进去时已付过费），不该再蹭一次免费
            if (AmbushZone.IsAmbushPlay(card))
            {
                return false;
            }

            Player? owner = base.Owner.Player;
            if (owner?.PlayerCombatState == null)
            {
                return false;
            }

            decimal energy = owner.PlayerCombatState.Energy;
            if (energy >= cost)
            {
                return false;
            }

            shortfall = cost - energy;
            return true;
        }
    }
}