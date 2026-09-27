using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core
{
    /// <summary>
    /// 循迹：**1 费蓝卡攻击**。造成伤害；**只要它躺在弃牌堆里，每个回合开始时会自动免费进入伏击区域**
    /// （下一回合被带出打掉 → 再回弃牌堆 → 再自动埋，形成循环）。
    /// <para>
    /// **2026-09-24 新增（用户定案）**：给伏击流一张"自己会回来的弹药"，把"埋 → 带出"的循环
    /// 从"每回合要亲手埋一张"变成"有一张牌自己在循环"，前期手牌不够时尤其明显。
    /// </para>
    /// <para>
    /// ⚠️ **绝不能自带[gold]消耗[/gold]**：消耗会把牌送进消耗牌堆而不是弃牌堆，
    /// 回收条件（`Pile.Type == PileType.Discard`）就永远不成立、卡直接变成废件——这是这张卡的设计红线。
    /// </para>
    /// <para>
    /// **回合开始的回收点是 `AfterPlayerTurnStart`**：本体的 `Hook.IterateHookListeners`
    /// 会把**所有战斗牌堆里的牌**（含弃牌堆）都当成钩子监听者，所以牌虽然在弃牌堆里，这个钩子照样会跑到它。
    /// 埋入复用 <see cref="AmbushZone.BuryFromDiscardAsync" />——区域已满 / 冷却中时它会返回 false，
    /// 牌就老实待在弃牌堆（下个回合再试），不做额外保护。
    /// </para>
    /// </summary>
    public sealed class FollowTheTrail : CardModel, ILingdawnpaoCard
    {
        protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DamageVar(6m, ValueProp.Move),
        ];

        public FollowTheTrail()
            : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        {
        }

        protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCardCompat(this, cardPlay).Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        /// <summary>回合开始时：若自己在弃牌堆里，就免费回到伏击区域。</summary>
        public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        {
            if (player != Owner || Pile?.Type != PileType.Discard)
            {
                return;
            }

            if (await AmbushZone.BuryFromDiscardAsync(choiceContext, player, this))
            {
                Log.Info("循迹：回合开始时从弃牌堆自动进入伏击区域");
            }
        }

        protected override void OnUpgrade()
        {
            DynamicVars.Damage.UpgradeValueBy(3m); // 6 → 9
        }
    }
}