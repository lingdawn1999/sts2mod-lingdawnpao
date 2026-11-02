using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>0费、消耗。复制伏击区中一张牌到手牌，原牌留区；升级复制两张。</summary>
public sealed class IceMirror : CardModel, ILingdawnpaoCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [new HoverTip(new LocString("card_keywords", "AMBUSH.title"), new LocString("card_keywords", "AMBUSH.description"))];

    public IceMirror() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? picked = await AmbushZone.SelectOneAsync(choiceContext, Owner, SelectionScreenPrompt);
        if (picked == null) return;
        for (int i = 0; i < DynamicVars.Cards.IntValue; i++)
        {
            CardModel? clone = CloneBuriedCard(picked);
            if (clone == null) break;
            await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Hand, Owner);
        }
    }

    internal CardModel? CloneBuriedCard(CardModel source)
    {
        AmbushZoneState zone = AmbushZone.For(Owner);
        int index = zone.Cards.ToList().IndexOf(source);
        if (index < 0) return null;
        // CreateClone rejects non-combat piles. Detach synchronously and restore in finally;
        // no events, awaits, reordered cards, stat changes or changes to burial history.
        zone.Pile.RemoveInternal(source, silent: true);
        try { return source.CreateClone(); }
        finally { zone.Pile.AddInternal(source, index, silent: true); }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
