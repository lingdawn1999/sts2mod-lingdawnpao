using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core.Relics
{
    /// <summary>
    /// 专属遗物「小零食」（白·普通）：每场战斗当你首次将牌加入[gold]伏击区域[/gold]时，抽 1 张牌。
    /// 2026-09-27 收官补充批——伏击体系的运转润滑件。
    /// "将牌加入"走与「伏击本能」相同的三条埋牌入口（手牌埋入 / 生成牌埋入 / 扑击抽牌堆顶），
    /// 全都算；用"每场战斗首次"判重（<see cref="BeforeCombatStart" /> 重置标记）。
    /// </summary>
    public sealed class CatSnack : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Common;

        private bool _triggeredThisCombat;

        public override async Task BeforeCombatStart()
        {
            _triggeredThisCombat = false;
            await Task.CompletedTask;
        }

        /// <summary>埋牌入口回调（由 AmbushZone 三条埋牌路径统一调用）：本场首次则抽 1 张牌。</summary>
        public static async Task NotifyBuriedAsync(PlayerChoiceContext choiceContext, Player player)
        {
            CatSnack? snack = player.GetRelic<CatSnack>();
            if (snack == null || snack._triggeredThisCombat)
            {
                return;
            }

            snack._triggeredThisCombat = true;
            snack.Flash();
            Log.Info("小零食：本场战斗首次埋牌，抽 1 张牌");
            await CardPileCmd.Draw(choiceContext, 1m, player);
        }
    }
}
