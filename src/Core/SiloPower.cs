using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 囤粮（玩家增益，由「囤粮」金卡赋予，Counter = 每回合抽牌上限）：
    /// **每当你的牌加入伏击区域，抽 1 张牌（每回合至多等同层数次）**。
    /// <para>
    /// 对位皮卡丘"打出羁绊牌摸牌"的体系过牌金卡——埋牌动作自带手牌回流，
    /// 玩家从此"埋牌不亏卡"，敢放开喂营地（2026-09-26 v2 重做新增）。
    /// </para>
    /// <para>
    /// 防刷设计（照 <see cref="KillerInstinctPower" /> 的 TurnNumber 记账法）：
    /// 每回合计数器在跨回合时清零；每回合至多 <see cref="PowerModel.Amount" /> 次。
    /// 稳态下额度只有首张 1 张/回合 → 稳态 1 抽/回合；想多吃必须投入
    /// 主动埋卡件（猎手布局/伏击斩/双线布置）或雪场类入流引擎——引擎强度来自构筑投入。
    /// 由 <see cref="AmbushZone" /> 的三条埋入路径（改道/生成/手牌）统一调用。
    /// </para>
    /// </summary>
    public sealed class SiloPower : PowerModel
    {
        private int _turn = -1;

        private int _drawnThisTurn;

        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Counter;

        /// <summary>埋入路径（改道/生成/手牌）统一调用：本回合抽牌次数未满则抽 1。</summary>
        public static async Task NotifyBuriedAsync(PlayerChoiceContext choiceContext, Player player)
        {
            SiloPower? power = player.Creature.GetPower<SiloPower>();
            int? turn = player.PlayerCombatState?.TurnNumber;
            if (power == null || turn == null)
            {
                return;
            }

            if (power._turn != turn.Value)
            {
                power._turn = turn.Value;
                power._drawnThisTurn = 0;
            }

            if (power._drawnThisTurn >= power.Amount)
            {
                return;
            }

            power._drawnThisTurn++;
            power.Flash();
            await CardPileCmd.Draw(choiceContext, 1, player);
            Log.Info($"囤粮：本回合第 {power._drawnThisTurn} 次埋牌，抽 1 张牌（上限 {power.Amount}）");
        }
    }
}
