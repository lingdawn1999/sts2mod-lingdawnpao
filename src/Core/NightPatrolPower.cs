using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace lingdawnpao.Core;

/// <summary>以首次成功完成的带出结算成长；回合号闸门同时覆盖连扑、额外埋伏与嵌套带出。</summary>
public sealed class NightPatrolPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];

    private int _broughtOutTurn = -1;

    public static async Task NotifyBroughtOutAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (!AmbushZone.CanContinueCombat(player)) return;
        NightPatrolPower? power = player.Creature.GetPower<NightPatrolPower>();
        int? turn = player.PlayerCombatState?.TurnNumber;
        if (power == null || turn == null || power._broughtOutTurn == turn.Value) return;

        // 先记账，避免能力应用钩子引发嵌套带出时重复成长。
        power._broughtOutTurn = turn.Value;
        power.Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, power.Owner, power.Amount, power.Owner, null);
        if (AmbushZone.CanContinueCombat(player))
            await PowerCmd.Apply<DexterityPower>(choiceContext, power.Owner, power.Amount, power.Owner, null);
    }
}
