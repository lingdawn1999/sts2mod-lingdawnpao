using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace lingdawnpao.Core;

/// <summary>先获得格挡，再自选带出一张库存；不额外触发遗物的自动带出，也不免除埋伏等待。</summary>
public sealed class CloseTheNet : CardModel, ILingdawnpaoCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description"))];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(24m, ValueProp.Move)];
    public CloseTheNet() : base(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (!AmbushZone.CanContinueCombat(Owner) || AmbushZone.For(Owner).IsCoolingDown) return;
        CardModel? chosen = await AmbushZone.SelectOneAsync(choiceContext, Owner,
            new LocString("cards", "CLOSE_THE_NET.selectionScreenPrompt"));
        if (chosen != null)
            await AmbushZone.BringOutChosenAsync(choiceContext, cardPlay.Target, Owner, chosen);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(6m);
}
