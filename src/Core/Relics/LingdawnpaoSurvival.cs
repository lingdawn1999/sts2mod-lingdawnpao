using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core.Relics;

/// <summary>Shared early-game buffer for manual burial and successful enemy Frostbite application.</summary>
public sealed class LingdawnpaoSurvival : RelicModel
{
    // 2026-10-28 用户定稿：从"开局自带（Rarity.None）"挪进**白色（Common）遗物池**——
    // 开局保底位置让给伏击者的"首回合自选入区"（见 FrostFang.AfterPlayerTurnStart），
    // 本遗物变成可获取的收集期防御强化。
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override string BigIconPath => "res://images/relics/lingdawnpao_survival.svg";
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<FrostbitePower>()];
    private bool _usedThisTurn;

    public override Task BeforeCombatStart()
    {
        _usedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner) _usedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisTurn = false;
        return Task.CompletedTask;
    }

    internal Task AfterManualBuryAsync(PlayerChoiceContext choiceContext, Player player) => GrantBlockAsync(player);

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource) =>
        amount > 0m && power is FrostbitePower && power.Owner.IsEnemy && applier == Owner.Creature
            ? GrantBlockAsync(Owner) : Task.CompletedTask;

    private async Task GrantBlockAsync(Player player)
    {
        if (player != Owner || _usedThisTurn || !AmbushZone.CanContinueCombat(player)) return;
        // Both routes share this flag; set it before awaiting block-triggered effects.
        _usedThisTurn = true;
        if (!TestMode.IsOn) Flash();
        await CreatureCmd.GainBlock(player.Creature, 3m, ValueProp.Unpowered, null, fast: true);
    }
}
