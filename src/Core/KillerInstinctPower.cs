using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 伏击本能（金卡能力，改名前叫「猎杀本能」）：**每回合各首次**结算的双补运转件——
    /// 本回合**首次把牌加入**[gold]伏击区域[/gold]时获得 <see cref="EnergyGain" /> 点[gold]能量[/gold]；
    /// 本回合**首次从伏击区域带出牌**时抽 <see cref="PowerModel.Amount" /> 张牌。
    /// <para>
    /// **2026-09-23 用户整张重做（原版是"每埋一张牌对随机敌人造成 6 伤"，2 费金卡）**：
    /// 起因是用户诊断"缺乏**稳定加费卡**和**稳定过牌卡**"——一次性 +1 费（「蓄能」）杯水车薪，
    /// 需要的是像本体「薪火之源」`Pyre` 那样**每回合都到账**的东西。这一张同时覆盖两条轴：
    /// 本体要两张三费才凑齐（薪火之源 2 费 = 能量上限 +1/回合；机器学习 1 费 = 每回合多抽 1 张），
    /// 本卡 2 费一次给全。
    /// </para>
    /// <para>
    /// ⚠️ **两条触发都必须是"每回合首次"，这是防刷的唯一闸门，不要改成"每次"**：
    /// 埋牌与带出这两个动作，一个回合内可以发生很多次（「设伏」一次埋 2 张、「倾泄」一次带出一串、
    /// 右键切形态还能反复倒手）——按次数结算就会被免费燃料刷爆（这正是「暗夜收割」前两版的死因）。
    /// 现在无论一回合埋 1 张还是 5 张、带出 1 张还是 5 张，两条各只结算一次，燃料彻底失去套利价值。
    /// </para>
    /// <para>
    /// **记账用 `PlayerCombatState.TurnNumber` 而不是回合开始钩子清零**：钩子漏跑（战斗流程中断、
    /// 玩家放弃对局等）只会让记账"过期一回合"，下一次结算自然照常；用钩子清零则可能永久卡死。
    /// 记账存在能力实例上——能力是按生物（每个玩家各一份）持有的，联机天然各算各的。
    /// </para>
    /// <para>
    /// 埋牌不走引擎钩子，因此两条触发都由 `AmbushZone` 主动调用：
    /// <see cref="NotifyBuriedAsync" /> 挂在两条埋牌入口（手牌埋入 <c>BuryAsync</c> 与生成牌埋入
    /// <c>BuryGeneratedAsync</c>，都算"加入牌"）；<see cref="NotifyBroughtOutAsync" /> 挂在带出唯一出口
    /// <c>BringOutFirstAsync</c>（遗物自动带出、「倾泄」「决堤」「猛虎连击」的带出全部经过它）。
    /// </para>
    /// </summary>
    public sealed class KillerInstinctPower : PowerModel
    {
        /// <summary>本回合首次埋牌给的固定能量。刻意不随升级变化——本卡的升级买的是摸牌数（见卡面）。</summary>
        public const int EnergyGain = 1;

        public override PowerType Type => PowerType.Buff;

        /// <summary>Counter：层数 = 每回合首次带出时抽的牌数（由卡面在打出的那一刻写入）。</summary>
        public override PowerStackType StackType => PowerStackType.Counter;

        protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [
            HoverTipFactory.ForEnergy(this),
        ];

        // 两条触发各自的"最近一次结算的回合号"，-1 = 本场战斗还没结算过。
        private int _buriedTurn = -1;

        private int _broughtOutTurn = -1;

        /// <summary>把牌加入伏击区域时调用（两条埋牌入口都算）。每回合只结算第一次。</summary>
        public static async Task NotifyBuriedAsync(PlayerChoiceContext choiceContext, Player player)
        {
            KillerInstinctPower? power = player.Creature.GetPower<KillerInstinctPower>();
            int? turn = player.PlayerCombatState?.TurnNumber;
            if (power == null || turn == null || power._buriedTurn == turn.Value)
            {
                return;
            }

            power._buriedTurn = turn.Value;
            power.Flash();
            await PlayerCmd.GainEnergy(EnergyGain, player);
            Log.Info($"伏击本能：本回合首次埋牌，获得 {EnergyGain} 点能量（回合 {turn.Value}）");
        }

        /// <summary>从伏击区域带出牌时调用（`BringOutFirstAsync` 是带出的唯一出口）。每回合只结算第一次。</summary>
        public static async Task NotifyBroughtOutAsync(PlayerChoiceContext choiceContext, Player player)
        {
            KillerInstinctPower? power = player.Creature.GetPower<KillerInstinctPower>();
            int? turn = player.PlayerCombatState?.TurnNumber;
            if (power == null || turn == null || power._broughtOutTurn == turn.Value)
            {
                return;
            }

            power._broughtOutTurn = turn.Value;
            power.Flash();
            await CardPileCmd.Draw(choiceContext, power.Amount, player);
            Log.Info($"伏击本能：本回合首次带出牌，抽 {power.Amount} 张（回合 {turn.Value}）");
        }
    }
}