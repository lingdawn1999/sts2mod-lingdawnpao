using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 先古遗物「暗杀者」：初始遗物「伏击者」的先古版本——玩家在本体先古事件里拿到「欧洛巴斯之触」
    /// 时，伏击者会被替换成本遗物（映射补丁见 Patches.cs 的 TouchOfOrobas_GetUpgradedStarterRelic_Patch）。
    /// 因为替换会把伏击者移除，所以它**继承伏击者的全部行为**（右键切换伏击形态、非伏击形态打出时带出一张、
    /// 战斗结束清空区域），再叠加两条新效果：
    /// ① [gold]伏击区域[/gold]上限 +1（由 AmbushZoneState.Capacity 读取，随身携带期间永久）；
    /// ② 在你的回合开始时，可以从手牌中自选一张牌**免费**加入[gold]伏击区域[/gold]（可选、能跳过）。
    ///
    /// 钩子选 AfterPlayerTurnStart 的原因：本体回合开始的顺序是
    /// 「清格挡 → BeforeSideTurnStart → 能量重置 → 摸牌 → AfterPlayerTurnStart → AfterSideTurnStart」，
    /// 这个钩子既能拿到刚摸的牌（满足"先摸牌再选"），又带 PlayerChoiceContext 可以弹选牌界面
    /// （本体 WellLaidPlansPower / GamblingChip 就是在这里做手牌选择的）。
    /// 埋入走 AmbushZone.BuryAsync 的 spendResources: false（免费），复用同一套动画与增益重算。
    /// 图标直接复用伏击者：只改 IconBaseName，PackedIconPath / PackedIconOutlinePath / BigIconPath 一起跟着走。
    /// </summary>
    public class Assassin : FrostFang
    {
        /// <summary>伏击区域上限加成。</summary>
        public const int AmbushCapacityBonus = 1;

        public override RelicRarity Rarity => RelicRarity.Ancient;

        protected override string IconBaseName => "frost_fang";

        public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        {
            if (player != Owner || player.PlayerCombatState == null)
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
            Log.Info($"暗杀者：回合开始自选埋牌 {picked[0].Id.Entry}（玩家 {player.NetId}）");
            await AmbushZone.BuryAsync(choiceContext, picked[0], player, spendResources: false);
        }
    }
}