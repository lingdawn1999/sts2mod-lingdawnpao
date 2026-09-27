using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core.Relics
{
    /// <summary>
    /// 专属遗物「雪球袋」（金·稀有）：每回合开始时，将 1 张[gold]雪球[/gold]加入手牌。
    /// 2026-09-27 收官批——雪球线的供弹引擎，与「雪场」（回合开始加雪球）同钩子同写法
    /// （<see cref="BeforeHandDraw" />，初始抽牌前即生效，第一回合就有弹可用）。
    /// </summary>
    public sealed class SnowballPouch : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Rare;

        public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
        {
            // 联机过滤：本钩子对所有玩家各跑一次，只给自己的回合供弹
            if (player != Owner)
            {
                return;
            }

            await Snowball.CreateIntoHandAsync(player, 1, combatState);
        }
    }
}
