using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace lingdawnpao.Core;

/// <summary>
/// 抢占先机：0费罕见技能，固有、消耗。免费将1（升级2）张手牌置入伏击区域，**且立即视为已潜伏满一回合**
/// （带出时立刻吃到埋伏段收益）。
/// <para>
/// **2026-10-28 用户重做**：原效果（开局免费埋牌）让位给初始遗物的"首回合自选入区"，
/// 本卡改成**救急通道**——燃眉之急需要立刻兑现收益时，当回合埋的当回合就能带着埋伏段打出来。
/// 代价合理（0 费 + 消耗 + 卡本身没了），是玩家主动选择的一次性爆发，
/// 与 2026-09-23 防白嫖改掉的"免费切形态反复埋-带"不是一回事（那是可循环，这是一次性资源）。
/// </para>
/// </summary>
public sealed class LingdawnpaoInitiative : CardModel, ILingdawnpaoCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public LingdawnpaoInitiative() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    private bool CanSelectHand(CardModel card) => card != this && card.Owner == Owner
        && card.Pile?.Type == PileType.Hand && AmbushStance.IsSelectable(card);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        AmbushZoneState zone = AmbushZone.For(Owner);
        if (!AmbushZone.CanContinueCombat(Owner) || zone.IsCoolingDown || zone.IsFull) return;
        var hand = PileType.Hand.GetPile(Owner).Cards.Where(CanSelectHand).ToList();
        int count = Math.Min(DynamicVars.Cards.IntValue, Math.Min(hand.Count, zone.Capacity - zone.Count));
        if (count <= 0) return;

        var selected = hand.Count == count ? hand : (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(new LocString("cards", "LINGDAWNPAO_INITIATIVE.selectionScreenPrompt"), count)
            { PretendCardsCanBePlayed = true }, CanSelectHand, this))
            .Where(CanSelectHand).Distinct().Take(count).ToList();
        foreach (CardModel card in selected)
        {
            if (!AmbushZone.CanContinueCombat(Owner) || zone.IsCoolingDown || zone.IsFull) break;
            if (CanSelectHand(card))
            {
                await AmbushZone.BuryAsync(choiceContext, card, Owner, spendResources: false);
                // 立即解锁：埋入记账改为上一回合，潜伏判定（IsReady）直接通过
                AmbushZone.MarkReadyImmediately(Owner, card);
            }
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}
