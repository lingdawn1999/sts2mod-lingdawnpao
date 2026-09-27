using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace lingdawnpao.Core.Relics
{
    /// <summary>
    /// 专属遗物「霜铃」（蓝·罕见）：每回合你首次造成[gold]冰冻[/gold]时，抽 1 张牌。
    /// 2026-09-27 收官批——冰冻线的过牌引擎，与「雪隐」共用同一条挂冰回调
    /// （<see cref="FrostbitePower.ApplyFrostAsync" /> 顶层 + 批量入口各回调一次，按"每次施加事件"计）。
    /// "每回合首次"用回合号判重：回调时比对 <see cref="_lastNotifiedTurn" />，同一回合内静默跳过。
    /// 回合号跨战斗会重数（每场从 1 计），所以 <see cref="BeforeCombatStart" /> 里重置。
    /// </summary>
    public sealed class Frostbell : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Uncommon;

        private int _lastNotifiedTurn = -1;

        public override async Task BeforeCombatStart()
        {
            _lastNotifiedTurn = -1;
            await Task.CompletedTask;
        }

        /// <summary>挂冰入口回调（由 FrostbitePower 统一调用）：本回合首次则抽 1 张牌。</summary>
        public static async Task NotifyFrostAppliedAsync(PlayerChoiceContext choiceContext, Creature applier)
        {
            Player? player = applier.Player;
            Frostbell? bell = player?.GetRelic<Frostbell>();
            if (bell == null || player == null)
            {
                return;
            }

            int turn = player.PlayerCombatState?.TurnNumber ?? -1;
            if (turn < 0 || turn == bell._lastNotifiedTurn)
            {
                return;
            }

            bell._lastNotifiedTurn = turn;
            bell.Flash();
            Log.Info("霜铃：本回合首次挂冰，抽 1 张牌");
            await CardPileCmd.Draw(choiceContext, 1m, player);
        }
    }
}
