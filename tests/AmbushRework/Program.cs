using System.Reflection;
using System.Runtime.CompilerServices;
using lingdawnpao.Core;
using lingdawnpao.Core.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

TestMode.IsOn = true;
int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
}

// A player's collection survives repeated resolutions, but not battle reset.
var ledger = new AmbushEffectLedger();
ledger.Record(new(AmbushEffectKind.SneakAttack, 7));
ledger.Record(new(AmbushEffectKind.SneakAttack, 7));
Check(ledger.Count == 1, "multiple copies and repeated triggers count once");
ledger.Record(new(AmbushEffectKind.HoldBreath, 3, 1));
var beforeUpgrade = ledger.Snapshot();
ledger.Record(new(AmbushEffectKind.SneakAttack, 9, Upgraded: true));
ledger.Record(new(AmbushEffectKind.SneakAttack, 7));
Check(ledger.Count == 2 && ledger.Snapshot()[0].Amount == 9, "upgraded trigger replaces weaker record, never downgrades");
Check(beforeUpgrade[0].Amount == 7, "in-flight resolution uses an immutable snapshot");
Check(ledger.Snapshot()[1].Kind == AmbushEffectKind.HoldBreath, "first-trigger order is stable");
Check(new AmbushEffectLedger().Count == 0, "another player does not share the ledger");
Check(AmbushEffectLedger.ResolutionCount(ledger.Snapshot()) == 1, "no Whet Claw means one resolution");
ledger.Record(new(AmbushEffectKind.WhetClaw));
Check(AmbushEffectLedger.ResolutionCount(ledger.Snapshot()) == 2, "Whet Claw adds exactly one non-recursive resolution");
Check(ledger.Count == 3, "resolving does not collect effects or consume the ledger");
ledger.Clear();
Check(ledger.Count == 0, "battle reset clears collection");

// Exercise actual card models and engine cost modifiers, not copies of their formulas.
foreach (Type type in new[] { typeof(AmbushBarrage), typeof(WhetClaw), typeof(ChainClaw),
    typeof(Blizzard), typeof(FrostFangCard), typeof(FrozenAdeptCard), typeof(FrostCoat),
    typeof(LingdawnpaoInitiative) }) ModelDb.Inject(type);
var barrage = (AmbushBarrage)ModelDb.Card<AmbushBarrage>().ToMutable();
Check(barrage.EnergyCost.Canonical == 0 && barrage.Keywords.Contains(CardKeyword.Retain)
    && !barrage.Keywords.Contains(CardKeyword.Exhaust), "barrage costs 0, retains, and does not exhaust");
Check(barrage.DynamicVars.CalculationBase.BaseValue == 5 && barrage.DynamicVars.ExtraDamage.BaseValue == 4,
    "barrage base damage and growth");
barrage.UpgradeInternal();
Check(barrage.DynamicVars.CalculationBase.BaseValue == 8 && barrage.DynamicVars.ExtraDamage.BaseValue == 6,
    "barrage upgrade raises both base damage and growth");
var syncCost = typeof(AmbushBarrage).GetMethod("ApplyCostGrowth", BindingFlags.NonPublic | BindingFlags.Instance)!;
int changes = 0;
barrage.EnergyCostChanged += () => changes++;
syncCost.Invoke(barrage, [3]);
syncCost.Invoke(barrage, [3]);
Check(barrage.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1 && changes == 1,
    "collection growth is applied once and refreshes the card UI");
// Clone a grown card: shallow state and cloned cost modifiers must agree.
var clone = (AmbushBarrage)barrage.MutableClone();
syncCost.Invoke(clone, [4]);
Check(clone.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
    && barrage.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1, "grown clone neither doubles growth nor changes original");
syncCost.Invoke(clone, [5]);
Check(clone.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2, "an odd extra effect does not increase cost");
syncCost.Invoke(clone, [6]);
Check(clone.EnergyCost.GetWithModifiers(CostModifiers.Local) == 3, "six effects increase cost by three");
syncCost.Invoke(barrage, [1]);
Check(barrage.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0, "the first effect leaves barrage free");
syncCost.Invoke(barrage, [0]);
Check(barrage.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0, "battle reset removes only collected cost growth");

var whet = ModelDb.Card<WhetClaw>().ToMutable();
Check(whet.EnergyCost.Canonical == 2 && whet.DynamicVars.Damage.BaseValue == 10
    && whet.DynamicVars["WhetClawPower"].BaseValue == 4, "Whet Claw costs 2 and deals 10 with growth 4");
whet.UpgradeInternal();
Check(whet.DynamicVars.Damage.BaseValue == 14 && whet.DynamicVars["WhetClawPower"].BaseValue == 5,
    "upgraded Whet Claw deals 14 with growth 5");
var fang = ModelDb.Card<FrostFangCard>().ToMutable();
Check(fang.DynamicVars["AmbushDamage"].BaseValue == 7 && fang.DynamicVars.HpLoss.BaseValue == 1,
    "Frost Fang bonus is 7 damage for 1 HP");
fang.UpgradeInternal();
Check(fang.DynamicVars["AmbushDamage"].BaseValue == 9, "upgraded Frost Fang bonus is 9");
var blizzard = ModelDb.Card<Blizzard>().ToMutable();
Check(blizzard.DynamicVars["FrostbitePower"].BaseValue == 8 && blizzard.DynamicVars["AmbushBonus"].BaseValue == 3,
    "Blizzard separates base 8 from bonus 3");
blizzard.UpgradeInternal();
Check(blizzard.DynamicVars["FrostbitePower"].BaseValue == 10 && blizzard.DynamicVars["AmbushBonus"].BaseValue == 4,
    "upgraded Blizzard separates base 10 from bonus 4");
var iceSeal = ModelDb.Card<FrozenAdeptCard>().ToMutable();
Check(iceSeal.Type == CardType.Power && iceSeal.EnergyCost.Canonical == 1 && iceSeal.DynamicVars["FrozenAdeptPower"].BaseValue == 1
    && !iceSeal.Keywords.Contains(CardKeyword.Exhaust), "Frost Echo is a one-cost power and adds one frost segment");
iceSeal.UpgradeInternal();
Check(iceSeal.Type == CardType.Power && iceSeal.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1
    && iceSeal.DynamicVars["FrozenAdeptPower"].BaseValue == 2 && !iceSeal.Keywords.Contains(CardKeyword.Exhaust),
    "Frost Echo upgrade adds a second extra segment while preserving one cost and no Exhaust");
var frostCoat = ModelDb.Card<FrostCoat>().ToMutable();
Check(frostCoat.EnergyCost.Canonical == 1 && frostCoat.Rarity == CardRarity.Common
    && frostCoat.DynamicVars["FrostbitePower"].BaseValue == 6 && !frostCoat.Keywords.Contains(CardKeyword.Exhaust),
    "Frost Coat remains a reusable one-cost common and now applies six frost");
frostCoat.UpgradeInternal();
Check(frostCoat.DynamicVars["FrostbitePower"].BaseValue == 9, "Frost Coat upgrade applies nine frost");
var initiative = ModelDb.Card<LingdawnpaoInitiative>().ToMutable();
Check(initiative.EnergyCost.Canonical == 0 && initiative.Type == CardType.Skill && initiative.Rarity == CardRarity.Uncommon
    && initiative.Keywords.Contains(CardKeyword.Innate) && initiative.Keywords.Contains(CardKeyword.Exhaust)
    && initiative.DynamicVars.Cards.IntValue == 1, "Seize the Initiative is a zero-cost uncommon with Innate and Exhaust, placing one card");
initiative.UpgradeInternal();
Check(initiative.DynamicVars.Cards.IntValue == 2 && initiative.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
    && initiative.Keywords.Contains(CardKeyword.Innate) && initiative.Keywords.Contains(CardKeyword.Exhaust),
    "Seize the Initiative upgrade places two cards without changing its keywords or cost");
Check(typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards." + nameof(LingdawnpaoInitiative)) == null,
    "the new card's class name does not collide with a vanilla model");
// Minimal managed player fixtures: no scene tree, combat commands, save files, or game process.
void SetField(object target, string name, object value) => target.GetType()
    .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
Player PlayerFixture(ulong id)
{
    var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
    SetField(player, "<NetId>k__BackingField", id);
    SetField(player, "<Creature>k__BackingField", new Creature(player, 72, 72));
    var combat = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
    SetField(combat, "_pets", new List<Creature>());
    var piles = new[] { new CardPile(PileType.Hand), new CardPile(PileType.Draw),
        new CardPile(PileType.Discard), new CardPile(PileType.Exhaust), new CardPile(PileType.Play) };
    SetField(combat, "_piles", piles);
    string[] pileProperties = ["Hand", "DrawPile", "DiscardPile", "ExhaustPile", "PlayPile"];
    for (int i = 0; i < piles.Length; i++) SetField(combat, $"<{pileProperties[i]}>k__BackingField", piles[i]);
    typeof(Player).GetProperty(nameof(Player.PlayerCombatState))!.SetValue(player, combat);
    SetField(player, "_runPiles", Array.Empty<CardPile>());
    return player;
}
var playerOne = PlayerFixture(1001);
var playerTwo = PlayerFixture(1002);
var inHand = (AmbushBarrage)ModelDb.Card<AmbushBarrage>().ToMutable();
inHand.Owner = playerOne;
playerOne.PlayerCombatState!.AllPiles[0].AddInternal(inHand);

ModelDb.Inject(typeof(ChainClawDiscountPower));
var discount = ModelDb.Power<ChainClawDiscountPower>().ToMutable();
typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(discount, playerOne.Creature);
typeof(PowerModel).GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(discount, 2);
Check(discount.TryModifyEnergyCostInCombat(inHand, 5, out decimal reduced) && reduced == 0,
    "each Chain Claw charge makes a fixed-cost attack free regardless of its cost");
Check(discount.TryModifyEnergyCostInCombat(inHand, 1, out reduced) && reduced == 0,
    "Chain Claw discount cannot create negative costs");
var skill = ModelDb.Card<Blizzard>().ToMutable();
skill.Owner = playerOne;
playerOne.PlayerCombatState.AllPiles[0].AddInternal(skill);
Check(!discount.TryModifyEnergyCostInCombat(skill, 2, out reduced) && reduced == 2,
    "Chain Claw does not discount skills");
var foreign = ModelDb.Card<ChainClaw>().ToMutable();
foreign.Owner = playerTwo;
playerTwo.PlayerCombatState!.AllPiles[0].AddInternal(foreign);
Check(!discount.TryModifyEnergyCostInCombat(foreign, 1, out reduced), "Chain Claw cannot discount a teammate's attack");

// Run the exact production barrage loop with observable command delegates.
foreach (var (echoes, hasWhet, expectedAttacks, expectedBonuses) in new[]
    { (0, false, 1, 1), (1, false, 1, 2), (0, true, 2, 2), (1, true, 2, 4), (2, true, 2, 6) })
{
    var entries = new List<RecordedAmbush> { new(AmbushEffectKind.AmbushPounce, 2),
        new(AmbushEffectKind.SneakAttack, 7), new(AmbushEffectKind.HoldBreath, 3, 1),
        new(AmbushEffectKind.ClawAssault, 1), new(AmbushEffectKind.Blizzard, 3),
        new(AmbushEffectKind.FrostFangCard, 7, 1) };
    if (hasWhet) entries.Add(new(AmbushEffectKind.WhetClaw));
    int attacks = 0;
    var resolved = new List<AmbushEffectKind>();
    AmbushEffectLedger.ResolveAsync(entries.ToArray(), 1 + echoes,
        () => { attacks++; return Task.CompletedTask; },
        effect => { resolved.Add(effect.Kind); return Task.CompletedTask; }, () => false).GetAwaiter().GetResult();
    Check(attacks == expectedAttacks, $"echo={echoes}, Whet={hasWhet}: base attack count is bounded");
    Check(new[] { AmbushEffectKind.SneakAttack, AmbushEffectKind.HoldBreath, AmbushEffectKind.ClawAssault,
        AmbushEffectKind.Blizzard, AmbushEffectKind.FrostFangCard }.All(kind => resolved.Count(x => x == kind) == expectedBonuses),
        $"echo={echoes}, Whet={hasWhet}: damage/block/energy/draw/frost/HP cost repeat exactly as planned");
    Check(!resolved.Contains(AmbushEffectKind.WhetClaw) && !resolved.Contains(AmbushEffectKind.AmbushPounce),
        "repeat and AoE instructions cannot recursively dispatch");
    Check(entries.Count == (hasWhet ? 7 : 6), "resolution neither collects nor consumes its input records");
}
int stoppedAttacks = 0, stoppedBonuses = 0;
AmbushEffectLedger.ResolveAsync([new(AmbushEffectKind.SneakAttack, 7), new(AmbushEffectKind.WhetClaw)], 2,
    () => { stoppedAttacks++; return Task.CompletedTask; },
    _ => { stoppedBonuses++; return Task.CompletedTask; }, () => stoppedAttacks > 0).GetAwaiter().GetResult();
Check(stoppedAttacks == 1 && stoppedBonuses == 0, "combat ending or owner dying stops remaining echoes and repeat");

// Check actual free-burial hooks and state changes, without a Godot scene tree.
ModelDb.Inject(typeof(EternalHuntPower));
ModelDb.Inject(typeof(AmbushEchoPower));
var huntPower = ModelDb.Power<EternalHuntPower>().ToMutable();
typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(huntPower, playerOne.Creature);
typeof(PowerModel).GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(huntPower, 1);
var playerZone = AmbushZone.For(playerOne);
int refreshedCosts = 0;
inHand.EnergyCostChanged += () => refreshedCosts++;
playerZone.IsAmbushMode = false;
AmbushZone.SyncCardCosts(playerOne);
Check(!huntPower.TryModifyEnergyCostInCombatLate(inHand, 9, out reduced) && reduced == 9,
    "normal form does not grant free burial");
playerZone.IsAmbushMode = true;
AmbushZone.SyncCardCosts(playerOne);
Check(huntPower.TryModifyEnergyCostInCombatLate(inHand, 9, out reduced) && reduced == 0 && refreshedCosts >= 2,
    "switching to ambush form immediately notifies hand costs and discounts even an expensive barrage");
SetField(huntPower, "_buriedThisTurn", 1);
Check(!huntPower.TryModifyEnergyCostInCombatLate(inHand, 9, out reduced), "used quota restores normal cost");
huntPower.BeforeSideTurnStart(null!, default, [playerOne.Creature], null!).GetAwaiter().GetResult();
Check(huntPower.TryModifyEnergyCostInCombatLate(inHand, 9, out reduced) && reduced == 0,
    "next turn resets and refreshes free-burial quota");
playerZone.IsCoolingDown = true;
Check(!huntPower.TryModifyEnergyCostInCombatLate(inHand, 9, out reduced), "cooldown cannot display a free burial");
playerZone.IsCoolingDown = false;
for (int i = 0; i < AmbushZone.MaxSize; i++) playerZone.Pile.AddInternal(ModelDb.Card<ChainClaw>().ToMutable());
Check(!huntPower.TryModifyEnergyCostInCombatLate(inHand, 9, out reduced), "a full zone cannot grant a free burial");
Check(!huntPower.TryModifyEnergyCostInCombatLate(foreign, 9, out reduced), "free burial cannot affect a teammate");
var echoPower = ModelDb.Power<AmbushEchoPower>().ToMutable();
typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(echoPower, playerOne.Creature);
typeof(PowerModel).GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(echoPower, 1);
playerOne.Creature.ApplyPowerInternal(echoPower);
Check(AmbushZone.AmbushTriggerCount(playerOne) == 2 && AmbushZone.AmbushTriggerCount(playerTwo) == 1,
    "actual Echo power adds one bonus trigger for its owner only");

// Latest AoE and zero-cost changes.
ModelDb.Inject(typeof(EarthshakerPalm));
ModelDb.Inject(typeof(SnowPeakCrush));
ModelDb.Inject(typeof(WildDogMilk));
var palm = ModelDb.Card<EarthshakerPalm>().ToMutable();
Check(palm.EnergyCost.Canonical == 1 && palm.TargetType == TargetType.AllEnemies
    && palm.DynamicVars.Damage.BaseValue == 6, "Earthshaker Palm is a 1-cost AoE with 6 damage per buried card");
palm.Owner = playerTwo;
var emptyPalm = (Task)typeof(EarthshakerPalm).GetMethod("OnPlay", BindingFlags.NonPublic | BindingFlags.Instance)!
    .Invoke(palm, [null, null])!;
emptyPalm.GetAwaiter().GetResult();
Check(emptyPalm.IsCompletedSuccessfully, "an empty zone skips attack rather than issuing zero hits");
palm.UpgradeInternal();
Check(palm.DynamicVars.Damage.BaseValue == 8, "upgraded Earthshaker Palm deals 8 per hit");
var peak = ModelDb.Card<SnowPeakCrush>().ToMutable();
Check(peak.TargetType == TargetType.AllEnemies && peak.DynamicVars.Damage.BaseValue == 8
    && peak.DynamicVars["FrostbitePower"].BaseValue == 12, "Snow Peak Crush targets all enemies with unchanged base values");
peak.UpgradeInternal();
Check(peak.DynamicVars.Damage.BaseValue == 10 && peak.DynamicVars["FrostbitePower"].BaseValue == 16,
    "Snow Peak Crush preserves its upgrade values");

// Scene-free checks for the two reported visual hangs.
var deathState = (MegaCrit.Sts2.Core.Animation.AnimState)typeof(Lingdawnpao)
    .GetMethod("CreateDeathState", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
Check(!deathState.IsLooping && deathState.NextState == null && deathState.Id == "idle_loop",
    "death keeps the placeholder pose but never loops or queues another state");
var milk = ModelDb.Card<WildDogMilk>().ToMutable();
var safeVisual = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.WildDogMilkVisualPatch")!;
var selectSafeVisual = safeVisual.GetMethod("AppliesTo", BindingFlags.Static | BindingFlags.NonPublic)!;
Check((bool)selectSafeVisual.Invoke(null, [milk])! && !(bool)selectSafeVisual.Invoke(null, [barrage])!,
    "the power fly fallback affects Wild Dog Milk only");
object?[] flyArguments = [milk, null];
bool runOriginalFly = (bool)safeVisual.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!
    .Invoke(null, flyArguments)!;
Check(!runOriginalFly && flyArguments[1] is Task { IsCompletedSuccessfully: true },
    "safe power visual returns a completed task in engine test mode");
Check(milk.Keywords.Contains(CardKeyword.Eternal) && milk.Type == CardType.Power,
    "the visual workaround preserves Eternal and the power-card gameplay type");
Check(typeof(CardModel).GetMethod("PlayPowerCardFlyVfx", BindingFlags.Instance | BindingFlags.NonPublic) != null,
    "the private visual patch target exists in the selected game branch");
var deadPlayer = PlayerFixture(1003);
SetField(deadPlayer, "<Creature>k__BackingField", new Creature(deadPlayer, 0, 72));
int deadRefreshes = 0;
var deadCard = ModelDb.Card<ChainClaw>().ToMutable();
deadCard.Owner = deadPlayer;
deadCard.EnergyCostChanged += () => deadRefreshes++;
deadPlayer.PlayerCombatState!.Hand.AddInternal(deadCard);
AmbushStance.RefreshHandVisuals(deadPlayer);
Check(deadRefreshes == 0, "power removal during death cannot refresh tearing-down hand nodes");
// Overload pays resources now and disables the whole region on the following turn.
foreach (Type type in new[] { typeof(Overload), typeof(OverloadPower), typeof(AmbushExhaustionPower), typeof(Snowdrift) }) ModelDb.Inject(type);
var overloadCard = ModelDb.Card<Overload>().ToMutable();
Check(overloadCard.EnergyCost.Canonical == 1 && overloadCard.Keywords.Contains(CardKeyword.Exhaust)
    && !overloadCard.Keywords.Contains(CardKeyword.Ethereal), "Overload costs one, exhausts and has no Ethereal");
overloadCard.UpgradeInternal();
Check(overloadCard.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
    && overloadCard.Keywords.Contains(CardKeyword.Exhaust), "Overload upgrade costs zero and still exhausts");
var drift = ModelDb.Card<Snowdrift>().ToMutable();
Check(drift.EnergyCost.Canonical == 2 && drift.Keywords.Contains(CardKeyword.Retain)
    && drift.Keywords.Contains(CardKeyword.Exhaust), "Snowdrift costs 2, retains and exhausts");
drift.UpgradeInternal();
Check(drift.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1
    && drift.DynamicVars["PlatingPower"].BaseValue == 1, "Snowdrift upgrade lowers cost without doubling conversion");
var overloadPlayer = PlayerFixture(1004);
var overloadZone = AmbushZone.For(overloadPlayer);
var attackResource = ModelDb.Card<ChainClaw>().ToMutable();
var skillResource = ModelDb.Card<Blizzard>().ToMutable();
overloadZone.Pile.AddInternal(attackResource);
overloadZone.Pile.AddInternal(skillResource);
AmbushZone.CountZoneTypes(overloadZone, out int overloadAttacks, out int overloadSkills);
Check(overloadAttacks == 1 && overloadSkills == 1, "ordinary zone supplies stats according to card type");
var exhaustion = ModelDb.Power<AmbushExhaustionPower>().ToMutable();
typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(exhaustion, overloadPlayer.Creature);
overloadPlayer.Creature.ApplyPowerInternal(exhaustion);
Check(overloadZone.AreBonusesSuppressed && overloadZone.IsCoolingDown,
    "exhaustion disables regional stats, burial and bring-out together");
Check(!AmbushZone.For(playerTwo).AreBonusesSuppressed, "exhaustion does not affect another player");
overloadZone.IsCoolingDown = false;
Check(overloadZone.IsCoolingDown, "clearing ordinary cooldown cannot erase exhaustion");
exhaustion.RemoveInternal();
Check(!overloadZone.AreBonusesSuppressed && !overloadZone.IsCoolingDown,
    "removing exhaustion restores zone availability and regional bonuses");
overloadZone.IsCoolingDown = true;
Check(overloadZone.AreBonusesSuppressed, "Concentration and Last Stand still suppress bonuses independently");
overloadZone.IsCoolingDown = false;
var convertFrost = typeof(Snowdrift).GetMethod("ConvertFrostToPlating", BindingFlags.Static | BindingFlags.NonPublic)!;
Check((decimal)convertFrost.Invoke(null, [0m])! == 0m && (decimal)convertFrost.Invoke(null, [1m])! == 0m,
    "Snowdrift does not grant fractional Plating from fewer than two frost stacks");
Check((decimal)convertFrost.Invoke(null, [3m])! == 1m && (decimal)convertFrost.Invoke(null, [20m])! == 10m,
    "Snowdrift converts total frost at a fixed two-to-one ratio rounded down");
// The damage and energy thresholds must advance together, not once per individual effect.
var thresholdCard = ModelDb.Card<AmbushBarrage>().ToMutable();
thresholdCard.Owner = overloadPlayer;
var multiplier = (Func<CardModel, Creature?, decimal>)typeof(MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedVar)
    .GetField("_multiplierCalc", BindingFlags.Instance | BindingFlags.NonPublic)!
    .GetValue(thresholdCard.DynamicVars.CalculatedDamage)!;
overloadZone.Effects.Clear();
Check(multiplier(thresholdCard, null) == 0 && thresholdCard.DynamicVars.CalculationBase.BaseValue == 5,
    "barrage begins at five damage with zero growth steps");
overloadZone.Effects.Record(new(AmbushEffectKind.SneakAttack, 7));
Check(multiplier(thresholdCard, null) == 0, "one effect grants no partial damage growth");
overloadZone.Effects.Record(new(AmbushEffectKind.HoldBreath, 3, 1));
Check(multiplier(thresholdCard, null) == 1 && thresholdCard.DynamicVars.ExtraDamage.BaseValue == 4,
    "two effects grant one four-damage step");
overloadZone.Effects.Record(new(AmbushEffectKind.WhetClaw));
Check(multiplier(thresholdCard, null) == 1, "three effects still grant only one growth step");
thresholdCard.UpgradeInternal();
Check(multiplier(thresholdCard, null) == 1 && thresholdCard.DynamicVars.CalculationBase.BaseValue == 8
    && thresholdCard.DynamicVars.ExtraDamage.BaseValue == 6, "upgraded barrage has eight base plus six per pair");
// Automatic attacks enter the Play pile too, but must leave the discount for a manual attack.
playerOne.Creature.ApplyPowerInternal(discount);
var automaticAttack = ModelDb.Card<ChainClaw>().ToMutable();
automaticAttack.Owner = playerOne;
playerOne.PlayerCombatState!.PlayPile.AddInternal(automaticAttack);
CardPlay PlayFixture(CardModel card, bool isAutoPlay)
{
    var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
    typeof(CardPlay).GetProperty(nameof(CardPlay.Card))!.SetValue(play, card);
    typeof(CardPlay).GetProperty(nameof(CardPlay.IsAutoPlay))!.SetValue(play, isAutoPlay);
    typeof(CardPlay).GetProperty("Player")?.SetValue(play, card.Owner);
    return play;
}
var autoPlay = PlayFixture(automaticAttack, true);
discount.BeforeCardPlayed(autoPlay).GetAwaiter().GetResult();
Check(playerOne.Creature.HasPower<ChainClawDiscountPower>() && discount.Amount == 2,
    "automatic attacks in the Play pile do not consume the discount");
Check(discount.TryModifyEnergyCostInCombat(inHand, 5, out reduced) && reduced == 0,
    "the manual hand attack remains free after an automatic attack");
// Charge consumption, native payment, burial and expiry require an active
// combat; exercise those in RegionalPlanningChecks instead of this bare fixture.
discount.RemoveInternal();
// Retain removal preserves the two cards' costs, block and upgrades.
foreach (Type type in new[] { typeof(TrapRecovery), typeof(SpinosaurusForm), typeof(Devour) }) ModelDb.Inject(type);
var recovery = ModelDb.Card<TrapRecovery>().ToMutable();
Check(!recovery.Keywords.Contains(CardKeyword.Retain) && recovery.EnergyCost.Canonical == 1, "Trap Recovery no longer retains");
recovery.UpgradeInternal();
Check(!recovery.Keywords.Contains(CardKeyword.Retain) && recovery.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
    "Trap Recovery upgrade still lowers cost to zero without Retain");
var spino = ModelDb.Card<SpinosaurusForm>().ToMutable();
Check(!spino.Keywords.Contains(CardKeyword.Retain) && spino.DynamicVars.Block.BaseValue == 7,
    "Spinosaurus retains its seven Block but loses Retain");
spino.UpgradeInternal();
Check(!spino.Keywords.Contains(CardKeyword.Retain) && spino.DynamicVars.Block.BaseValue == 10,
    "upgraded Spinosaurus still has ten Block and no Retain");
// Ice Mirror uses real combat cloning, preserving the original buried card and its FIFO position.
foreach (Type type in new[] { typeof(IceMirror), typeof(Concentration), typeof(Regroup) }) ModelDb.Inject(type);
var mirror = ModelDb.Card<IceMirror>().ToMutable();
Check(mirror.EnergyCost.Canonical == 0 && mirror.Keywords.Contains(CardKeyword.Exhaust)
    && !mirror.Keywords.Contains(CardKeyword.Retain) && mirror.DynamicVars.Cards.BaseValue == 1,
    "Ice Mirror costs zero, exhausts, and creates one copy");
mirror.UpgradeInternal();
Check(mirror.DynamicVars.Cards.BaseValue == 2, "upgraded Ice Mirror creates two copies");
var mirrorPlayer = PlayerFixture(1005);
var mirrorCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
mirrorCombat.AddPlayer(mirrorPlayer);
var mirrorZone = AmbushZone.For(mirrorPlayer);
var beforeSource = ModelDb.Card<ChainClaw>().ToMutable();
var mirrorSource = ModelDb.Card<ChainClaw>().ToMutable();
var afterSource = ModelDb.Card<ChainClaw>().ToMutable();
foreach (var c in new[] { beforeSource, mirrorSource, afterSource })
{
    mirrorCombat.AddCard(c, mirrorPlayer);
    mirrorZone.Pile.AddInternal(c);
}
mirrorSource.UpgradeInternal();
mirrorSource.ExhaustOnNextPlay = true;
mirrorZone.BuriedTurn[mirrorSource] = -1;
mirror.Owner = mirrorPlayer;
var cloneBuried = typeof(IceMirror).GetMethod("CloneBuriedCard", BindingFlags.Instance | BindingFlags.NonPublic)!;
var copied = (CardModel)cloneBuried.Invoke(mirror, [mirrorSource])!;
Check(copied != mirrorSource && copied.CloneOf == mirrorSource && copied.Owner == mirrorPlayer,
    "Ice Mirror creates a real independent combat clone of the selected buried card");
Check(copied.IsUpgraded && copied.DynamicVars.Damage.BaseValue == mirrorSource.DynamicVars.Damage.BaseValue
    && copied.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1,
    "a copy preserves upgrades and normal cost instead of becoming free");
Check(mirrorZone.Cards.SequenceEqual(new[] { beforeSource, mirrorSource, afterSource })
    && mirrorZone.BuriedTurn[mirrorSource] == -1 && !mirrorZone.BuriedTurn.ContainsKey(copied),
    "cloning preserves FIFO and original burial age without giving the clone a burial record");
Check(!copied.ExhaustOnNextPlay && mirrorSource.ExhaustOnNextPlay,
    "cloning clears only the copy's pending replay-exhaust marker");
Check(!AmbushZone.IsVeteranAmbush(copied) && !AmbushZone.IsReadyForAmbush(copied),
    "the copy cannot inherit the source's Ambush eligibility");
var copiedAgain = (CardModel)cloneBuried.Invoke(mirror, [mirrorSource])!;
Check(copiedAgain != copied && mirrorZone.Count == 3, "the second copy neither consumes nor duplicates the original in the zone");
Check(cloneBuried.Invoke(mirror, [inHand]) == null && mirrorZone.Count == 3,
    "a card outside the owner's Ambush zone cannot be copied");
var concentration = ModelDb.Card<Concentration>().ToMutable();
Check(concentration.EnergyCost.Canonical == 0 && concentration.Keywords.Contains(CardKeyword.Exhaust)
    && !concentration.Keywords.Contains(CardKeyword.Retain), "Concentration costs zero and exhausts without base Retain");
concentration.UpgradeInternal();
Check(concentration.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
    && concentration.Keywords.Contains(CardKeyword.Exhaust) && concentration.Keywords.Contains(CardKeyword.Retain),
    "Concentration upgrade adds Retain while preserving Exhaust and zero cost");
var regroupCard = ModelDb.Card<Regroup>().ToMutable();
Check(regroupCard.EnergyCost.Canonical == 1 && !regroupCard.Keywords.Contains(CardKeyword.Exhaust),
    "Regroup costs one and does not exhaust before upgrading");
regroupCard.UpgradeInternal();
Check(regroupCard.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
    && !regroupCard.Keywords.Contains(CardKeyword.Exhaust), "Regroup upgrade reduces its cost to zero and remains non-exhausting");
var handlesBringOut = typeof(FrostFang).GetMethod("HandlesOwnBringOut", BindingFlags.Static | BindingFlags.NonPublic)!;
Check((bool)handlesBringOut.Invoke(null, [concentration])! && (bool)handlesBringOut.Invoke(null, [mirror])!
    && (bool)handlesBringOut.Invoke(null, [regroupCard])!
    && (bool)handlesBringOut.Invoke(null, [initiative])!
    && !(bool)handlesBringOut.Invoke(null, [inHand])!,
    "regional planning cards suppress relic FIFO bring-out, including Seize the Initiative, while ordinary cards retain it");
var rejectedBringOut = AmbushZone.BringOutChosenAsync(null!, null, mirrorPlayer, inHand, ignoreAmbushDelay: true).GetAwaiter().GetResult();
Check(rejectedBringOut == null && mirrorZone.Cards.SequenceEqual(new[] { beforeSource, mirrorSource, afterSource }),
    "targeted bring-out rejects cards outside the zone without reordering resources");
mirrorZone.IsCoolingDown = true;
Check(AmbushZone.BringOutChosenAsync(null!, null, mirrorPlayer, mirrorSource, ignoreAmbushDelay: true).GetAwaiter().GetResult() == null
    && mirrorZone.Count == 3, "a disabled zone cannot be bypassed by targeted bring-out");
mirrorZone.IsCoolingDown = false;
// Godspeed now grants an unconditional turn; its region penalty belongs only to that turn.
ModelDb.Inject(typeof(DevourPower));
var godspeedCard = ModelDb.Card<Devour>().ToMutable();
Check(godspeedCard.EnergyCost.Canonical == 2 && godspeedCard.Keywords.Contains(CardKeyword.Exhaust)
    && !godspeedCard.Keywords.Contains(CardKeyword.Retain) && !godspeedCard.DynamicVars.ContainsKey("CardsRequired"),
    "Godspeed costs two and exhausts, with no resource threshold or base Retain");
godspeedCard.UpgradeInternal();
Check(godspeedCard.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
    && godspeedCard.Keywords.Contains(CardKeyword.Retain) && godspeedCard.Keywords.Contains(CardKeyword.Exhaust),
    "Godspeed upgrade adds Retain while preserving two cost and Exhaust");
var speedPlayer = PlayerFixture(1006);
var speedCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
speedCombat.AddPlayer(speedPlayer);
var speedZone = AmbushZone.For(speedPlayer);
var speed = (DevourPower)ModelDb.Power<DevourPower>().ToMutable();
typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(speed, speedPlayer.Creature);
typeof(PowerModel).GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(speed, 2);
speedPlayer.Creature.ApplyPowerInternal(speed);
Check(speed.ShouldTakeExtraTurn(speedPlayer) && !speed.ShouldTakeExtraTurn(playerTwo)
    && !speedZone.IsCoolingDown, "queued Godspeed turns belong only to their owner and leave the current zone usable");
speed.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [speedPlayer.Creature]).GetAwaiter().GetResult();
Check(speed.Amount == 2 && !speed.IsExtraTurnActive, "ending the casting turn does not spend a queued extra turn");
speed.AfterTakingExtraTurn(playerTwo).GetAwaiter().GetResult();
Check(!speedZone.IsCoolingDown && speed.Amount == 2, "a teammate's extra turn does not activate or spend Godspeed");
speed.AfterTakingExtraTurn(speedPlayer).GetAwaiter().GetResult();
Check(speed.IsExtraTurnActive && speedZone.IsCoolingDown && speedZone.AreBonusesSuppressed && speed.Amount == 2,
    "beginning the first extra turn disables burying, bring-out and regional stat bonuses");
speedZone.IsCoolingDown = false;
Check(speedZone.IsCoolingDown, "another effect clearing normal cooldown cannot erase the Godspeed penalty");
speed.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, [speedPlayer.Creature]).GetAwaiter().GetResult();
speed.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [playerTwo.Creature]).GetAwaiter().GetResult();
Check(speed.IsExtraTurnActive && speed.Amount == 2, "enemy and teammate turn ends cannot clear the owner's penalty");
speed.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [speedPlayer.Creature]).GetAwaiter().GetResult();
Check(!speed.IsExtraTurnActive && !speedZone.IsCoolingDown && speed.Amount == 1
    && speed.ShouldTakeExtraTurn(speedPlayer), "the first extra turn spends only one queued turn and restores the zone between turns");
speed.AfterTakingExtraTurn(speedPlayer).GetAwaiter().GetResult();
Check(speedZone.IsCoolingDown && speed.IsExtraTurnActive, "the second extra turn independently disables the zone again");
speed.AfterSideTurnEnd(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [speedPlayer.Creature]).GetAwaiter().GetResult();
Check(!speedPlayer.Creature.HasPower<DevourPower>() && !speedZone.IsCoolingDown
    && !speed.ShouldTakeExtraTurn(speedPlayer), "the last extra turn removes the status and leaves no disabled-zone residue");
// Verify actual stat removal/restoration and real region entry points, without burning resources.
ModelDb.Inject(typeof(MegaCrit.Sts2.Core.Models.Powers.StrengthPower));
ModelDb.Inject(typeof(MegaCrit.Sts2.Core.Models.Powers.DexterityPower));
var storedAttack = ModelDb.Card<ChainClaw>().ToMutable();
var storedSkill = ModelDb.Card<Snowdrift>().ToMutable();
speedCombat.AddCard(storedAttack, speedPlayer);
speedCombat.AddCard(storedSkill, speedPlayer);
speedZone.Pile.AddInternal(storedAttack);
speedZone.Pile.AddInternal(storedSkill);
var speedContext = new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext();
AmbushZone.RecomputeBonusesAsync(speedContext, speedPlayer).GetAwaiter().GetResult();
Check(speedZone.AppliedStr == 1 && speedZone.AppliedDex == 1, "ordinary stored attack and skill provide their regional stats");
var speedAgain = (DevourPower)ModelDb.Power<DevourPower>().ToMutable();
typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(speedAgain, speedPlayer.Creature);
typeof(PowerModel).GetField("_amount", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(speedAgain, 1);
speedPlayer.Creature.ApplyPowerInternal(speedAgain);
speedAgain.AfterTakingExtraTurn(speedPlayer).GetAwaiter().GetResult();
Check(speedZone.AppliedStr == 0 && speedZone.AppliedDex == 0 && speedZone.Count == 2,
    "Godspeed removes regional Strength and Dexterity but leaves stored cards intact");
Check(AmbushZone.BringOutFirstAsync(speedContext, null, speedPlayer).GetAwaiter().GetResult() == null
    && speedZone.Count == 2, "Godspeed's disabled zone cannot bring out a card");
AmbushZone.BuryAsync(speedContext, storedAttack, speedPlayer, spendResources: false).GetAwaiter().GetResult();
Check(speedZone.Count == 2, "Godspeed's disabled zone rejects even free effect burial");
MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<DevourPower>(speedContext, speedPlayer.Creature, 1m,
    speedPlayer.Creature, null).GetAwaiter().GetResult();
Check(speedAgain.Amount == 2 && speedZone.IsCoolingDown,
    "casting another Godspeed during an extra turn queues another turn without lifting the current penalty");
speedAgain.AfterSideTurnEnd(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [speedPlayer.Creature])
    .GetAwaiter().GetResult();
Check(speedAgain.Amount == 1 && speedZone.AppliedStr == 1 && speedZone.AppliedDex == 1,
    "finishing the extra turn restores original regional stats and preserves the newly queued turn");
speedAgain.AfterTakingExtraTurn(speedPlayer).GetAwaiter().GetResult();
Check(speedZone.AppliedStr == 0 && speedZone.AppliedDex == 0, "a turn queued during an extra turn also pays the complete regional penalty");
speedAgain.AfterSideTurnEnd(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [speedPlayer.Creature])
    .GetAwaiter().GetResult();
Check(speedZone.Cards.SequenceEqual(new[] { storedAttack, storedSkill }) && !speedZone.IsCoolingDown
    && speedZone.AppliedStr == 1 && speedZone.AppliedDex == 1,
    "all Godspeed turns finish with cards, FIFO order and stat bonuses restored");
// Concurrent burial rollback must remain per card and per owner.
var entriesType = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.PendingBuryEntries`1")!.MakeGenericType(typeof(string));
var burialEntries = Activator.CreateInstance(entriesType, nonPublic: true)!;
var registerEntry = entriesType.GetMethod("Register")!;
var takeEntry = entriesType.GetMethod("Take")!;
registerEntry.Invoke(burialEntries, [1UL, 10U, "first"]);
registerEntry.Invoke(burialEntries, [1UL, 11U, "second"]);
registerEntry.Invoke(burialEntries, [2UL, 10U, "teammate"]);
Check((string?)takeEntry.Invoke(burialEntries, [1UL, 10U]) == "first", "first burial consumes only its own visual entry");
Check((string?)takeEntry.Invoke(burialEntries, [1UL, 11U]) == "second", "second rejected burial still has its rollback entry after first succeeds");
Check(takeEntry.Invoke(burialEntries, [1UL, 11U]) == null, "a visual rollback is taken only once");
Check((string?)takeEntry.Invoke(burialEntries, [2UL, 10U]) == "teammate", "matching card indices on different players cannot steal rollback entries");
registerEntry.Invoke(burialEntries, [1UL, 12U, "old combat"]);
registerEntry.Invoke(burialEntries, [2UL, 12U, "other owner"]);
entriesType.GetMethod("Clear")!.Invoke(burialEntries, [1UL]);
Check(takeEntry.Invoke(burialEntries, [1UL, 12U]) == null && (string?)takeEntry.Invoke(burialEntries, [2UL, 12U]) == "other owner",
    "battle reset clears only the owner's pending visuals");
var epochType = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.CardVisualEpoch")!;
var epochGuard = Activator.CreateInstance(epochType, nonPublic: true)!;
int staleEpoch = (int)epochType.GetMethod("Renew")!.Invoke(epochGuard, null)!;
Check((bool)epochType.GetMethod("IsCurrent")!.Invoke(epochGuard, [staleEpoch])!, "an active visual callback owns the current node lease");
epochType.GetMethod("Renew")!.Invoke(epochGuard, null);
Check(!(bool)epochType.GetMethod("IsCurrent")!.Invoke(epochGuard, [staleEpoch])!,
    "pool reuse or hand return invalidates delayed callbacks even for the same card model");
// Regional stat reversal is bookkeeping: Artifact must neither block it nor be consumed.
ModelDb.Inject(typeof(MegaCrit.Sts2.Core.Models.Powers.ArtifactPower));
T GivePower<T>(Player player, int amount) where T : PowerModel
{
    var power = (T)ModelDb.Power<T>().ToMutable();
    typeof(PowerModel).GetProperty(nameof(PowerModel.Owner))!.SetValue(power, player.Creature);
    typeof(PowerModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, amount);
    player.Creature.ApplyPowerInternal(power);
    return power;
}
var artifactPlayer = PlayerFixture(1007);
SetField(artifactPlayer, "_relics", new List<RelicModel>());
SetField(artifactPlayer, "_potionSlots", new List<PotionModel?>());
SetField(artifactPlayer.PlayerCombatState!, "<OrbQueue>k__BackingField", new MegaCrit.Sts2.Core.Entities.Orbs.OrbQueue(artifactPlayer));
artifactPlayer.ActivateHooks();
var artifactCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
artifactCombat.AddPlayer(artifactPlayer);
var originalStrength = GivePower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(artifactPlayer, 5);
var originalDexterity = GivePower<MegaCrit.Sts2.Core.Models.Powers.DexterityPower>(artifactPlayer, 4);
var artifact = GivePower<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>(artifactPlayer, 1);
var regionAttack = ModelDb.Card<ChainClaw>().ToMutable();
var regionSkill = ModelDb.Card<Snowdrift>().ToMutable();
artifactCombat.AddCard(regionAttack, artifactPlayer);
artifactCombat.AddCard(regionSkill, artifactPlayer);
var artifactZone = AmbushZone.For(artifactPlayer);
artifactZone.Pile.AddInternal(regionAttack);
artifactZone.Pile.AddInternal(regionSkill);
AmbushZone.RecomputeBonusesAsync(speedContext, artifactPlayer).GetAwaiter().GetResult();
Check(originalStrength.Amount == 6 && originalDexterity.Amount == 5, "region adds only its own stats on top of permanent stats");
Check(artifact.TryModifyPowerAmountReceived(ModelDb.Power<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(),
    artifactPlayer.Creature, -1m, null, out var blockedRegionalDelta) && blockedRegionalDelta == 0m,
    "ordinary negative Strength applications would be blocked by Artifact");
artifactZone.Pile.RemoveInternal(regionAttack);
artifactZone.Pile.RemoveInternal(regionSkill);
AmbushZone.RecomputeBonusesAsync(speedContext, artifactPlayer).GetAwaiter().GetResult();
Check(originalStrength.Amount == 5 && originalDexterity.Amount == 4 && artifact.Amount == 1,
    "removing regional stats cannot leave permanent stat gains or spend Artifact");
Check(artifactZone.AppliedStr == 0 && artifactZone.AppliedDex == 0, "regional bookkeeping agrees with the actual remaining permanent stats");

// Overload debt starts before draw/start-turn effects, and refresh is independent of hook ordering.
var debtPlayer = PlayerFixture(1008);
var debtCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
debtCombat.AddPlayer(debtPlayer);
var debtZone = AmbushZone.For(debtPlayer);
foreach (bool overloadFirst in new[] { true, false })
{
    var oldDebt = GivePower<AmbushExhaustionPower>(debtPlayer, 1);
    oldDebt.AfterApplied(debtPlayer.Creature, null).GetAwaiter().GetResult();
    var nextOverload = GivePower<OverloadPower>(debtPlayer, 1);
    nextOverload.AfterApplied(debtPlayer.Creature, null).GetAwaiter().GetResult();
    debtPlayer.PlayerCombatState!.IncrementTurnNumber();
    if (!overloadFirst) oldDebt.BeforeSideTurnStart(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
        [debtPlayer.Creature], debtCombat).GetAwaiter().GetResult();
    nextOverload.BeforeSideTurnStart(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
        [debtPlayer.Creature], debtCombat).GetAwaiter().GetResult();
    if (overloadFirst) oldDebt.BeforeSideTurnStart(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
        [debtPlayer.Creature], debtCombat).GetAwaiter().GetResult();
    Check(debtZone.AreBonusesSuppressed && debtZone.IsCoolingDown,
        "overload debt disables the whole zone before draw regardless of hook ordering");
    var refreshedDebt = debtPlayer.Creature.GetPower<AmbushExhaustionPower>()!;
    Check(refreshedDebt != null && refreshedDebt.Amount == 1, "refreshing debt neither loses the penalty nor accumulates duplicate single-status stacks");
    refreshedDebt!.BeforeSideTurnStart(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
        [playerTwo.Creature], debtCombat).GetAwaiter().GetResult();
    Check(debtZone.AreBonusesSuppressed, "a teammate's turn cannot expire overload debt");
    debtPlayer.PlayerCombatState.IncrementTurnNumber();
    refreshedDebt.BeforeSideTurnStart(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
        [debtPlayer.Creature], debtCombat).GetAwaiter().GetResult();
    Check(!debtZone.AreBonusesSuppressed, "refreshed overload debt expires on the owner's following turn");
}
var canContinue = typeof(AmbushZone).GetMethod("CanContinueCombat", BindingFlags.Static | BindingFlags.NonPublic)!;
Check((bool)canContinue.Invoke(null, [debtPlayer])!, "an alive test player can continue an action");
Check(!(bool)canContinue.Invoke(null, [deadPlayer])!, "a dead player cannot continue generating or selecting cards");
Check(!AmbushZone.BuryGeneratedAsync(null!, deadPlayer, regionAttack).GetAwaiter().GetResult(),
    "generated burial rejects a dead player before mutating any piles");
Check(AmbushZone.SelectOneAsync(null!, deadPlayer, null!).GetAwaiter().GetResult() == null,
    "a dead player never opens a region selection screen");
try
{
    TestMode.IsOn = false;
    Check(!(bool)canContinue.Invoke(null, [debtPlayer])!, "outside a live combat, continuation guards stop follow-up selection and generation");
}
finally { TestMode.IsOn = true; }
// Execute checks must happen on application/growth even without a damage event.
ModelDb.Inject(typeof(FrostbitePower));
var frostPlayer = PlayerFixture(1009);
SetField(frostPlayer, "<Creature>k__BackingField", new Creature(frostPlayer, 6, 72));
var thresholdFrost = GivePower<FrostbitePower>(frostPlayer, 5);
int executes = 0;
typeof(FrostbitePower).GetField("_doomAction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(thresholdFrost,
    (Func<Creature, Task>)(async target =>
    {
        executes++;
        await thresholdFrost.AfterDamageReceived(speedContext, target, null!, default, null, null);
    }));
thresholdFrost.AfterPowerAmountChanged(speedContext, thresholdFrost, 1m, null, null).GetAwaiter().GetResult();
Check(executes == 0, "below-current-HP frost does not execute");
thresholdFrost.SetAmount(6);
thresholdFrost.AfterPowerAmountChanged(speedContext, thresholdFrost, 1m, null, null).GetAwaiter().GetResult();
Check(executes == 1, "frost equal to current HP executes on application without requiring damage");
Check(thresholdFrost.Amount == 6, "a stack-only execute check does not halve frost or deal an extra damage segment");
thresholdFrost.SetAmount(8);
thresholdFrost.AfterPowerAmountChanged(speedContext, thresholdFrost, 2m, null, null).GetAwaiter().GetResult();
Check(executes == 2, "growing an existing frost power past current HP immediately checks execution");
thresholdFrost.AfterPowerAmountChanged(speedContext, thresholdFrost, -1m, null, null).GetAwaiter().GetResult();
Check(executes == 2, "decay does not create a second stack-growth execute event");
thresholdFrost.AfterPowerAmountChanged(speedContext, artifact, 1m, null, null).GetAwaiter().GetResult();
Check(executes == 2, "changing an unrelated power does not retrigger frost execution");
Check(executes == 2, "damage hooks invoked inside execution cannot recursively execute the same target");
// Run the actual engine application hooks, with only the terminal kill action observed.
SetField(frostPlayer, "_relics", new List<RelicModel>());
SetField(frostPlayer, "_potionSlots", new List<PotionModel?>());
SetField(frostPlayer.PlayerCombatState!, "<OrbQueue>k__BackingField", new MegaCrit.Sts2.Core.Entities.Orbs.OrbQueue(frostPlayer));
frostPlayer.ActivateHooks();
var frostCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
frostCombat.AddPlayer(frostPlayer);
thresholdFrost.RemoveInternal();
var doomActionField = typeof(FrostbitePower).GetField("_doomAction", BindingFlags.Instance | BindingFlags.NonPublic)!;
var frostCanonical = ModelDb.Power<FrostbitePower>();
var originalDoomAction = doomActionField.GetValue(frostCanonical);
int applicationExecutes = 0;
var combatManager = MegaCrit.Sts2.Core.Combat.CombatManager.Instance;
Action EnterTestCombat(MegaCrit.Sts2.Core.Combat.CombatState state, bool inProgress, bool starting)
{
    var manager = MegaCrit.Sts2.Core.Combat.CombatManager.Instance;
    var type = manager.GetType();
    var turnField = type.GetField("_turnState", BindingFlags.Instance | BindingFlags.NonPublic);
    if (turnField != null)
    {
        var previous = turnField.GetValue(manager);
        var turn = Activator.CreateInstance(turnField.FieldType, [state])!;
        turnField.FieldType.GetProperty("IsInProgress")!.SetValue(turn, inProgress);
        turnField.FieldType.GetProperty("IsStarting")!.SetValue(turn, starting);
        turnField.SetValue(manager, turn);
        return () => turnField.SetValue(manager, previous);
    }
    var field = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!;
    var progress = type.GetProperty("IsInProgress")!;
    var start = type.GetProperty("IsStarting")!;
    var oldState = field.GetValue(manager);
    var oldProgress = progress.GetValue(manager);
    var oldStart = start.GetValue(manager);
    field.SetValue(manager, state);
    progress.SetValue(manager, inProgress);
    start.SetValue(manager, starting);
    return () => { field.SetValue(manager, oldState); progress.SetValue(manager, oldProgress); start.SetValue(manager, oldStart); };
}
var restoreFrostCombat = EnterTestCombat(frostCombat, false, true);
try
{
    doomActionField.SetValue(frostCanonical, (Func<Creature, Task>)(_ => { applicationExecutes++; return Task.CompletedTask; }));
    var appliedFrost = MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<FrostbitePower>(speedContext,
        frostPlayer.Creature, 6m, null, null).GetAwaiter().GetResult()!;
    Check(applicationExecutes == 1 && appliedFrost.Amount == 6,
        "actual first-application hooks execute once at the HP threshold without dealing damage or halving layers");
    MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(speedContext, appliedFrost, 1m, null, null).GetAwaiter().GetResult();
    Check(applicationExecutes == 2 && appliedFrost.Amount == 7,
        "actual stacking hooks execute immediately once, including direct layer increases");
}
finally
{
    doomActionField.SetValue(frostCanonical, originalDoomAction);
    restoreFrostCombat();
}
// Normal frost resolves damage and decay before executing. These exercise the
// actual application and damage commands, not a duplicate damage/decay formula.
ModelDb.Inject(typeof(WinterMeltPower));
ModelDb.Inject(typeof(FrozenAdeptPower));
var frostOrderPlayer = PlayerFixture(1011);
SetField(frostOrderPlayer, "<Creature>k__BackingField", new Creature(frostOrderPlayer, 84, 100));
SetField(frostOrderPlayer, "_runState", MegaCrit.Sts2.Core.Runs.NullRunState.Instance);
SetField(frostOrderPlayer, "_relics", new List<RelicModel>());
SetField(frostOrderPlayer, "_potionSlots", new List<PotionModel?>());
SetField(frostOrderPlayer.PlayerCombatState!, "<OrbQueue>k__BackingField", new MegaCrit.Sts2.Core.Entities.Orbs.OrbQueue(frostOrderPlayer));
frostOrderPlayer.ActivateHooks();
var frostOrderCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
frostOrderCombat.AddPlayer(frostOrderPlayer);
var restoreFrostOrderCombat = EnterTestCombat(frostOrderCombat, false, true);
var frostExecutions = new List<(int Hp, decimal Layers)>();
Func<Creature, Task> observeFrostExecution = target =>
{
    frostExecutions.Add((target.CurrentHp, target.GetPower<FrostbitePower>()!.Amount));
    return Task.CompletedTask;
};
try
{
    doomActionField.SetValue(frostCanonical, observeFrostExecution);
    var target = frostOrderPlayer.Creature;
    FrostbitePower ApplyNormalFrost(decimal amount) => FrostbitePower.ApplyFrostAsync(speedContext,
        target, amount, null, null).GetAwaiter().GetResult()!;

    target.SetCurrentHpInternal(74); // First Snow Peak's ordinary 10 damage.
    var frost = ApplyNormalFrost(16);
    Check(target.CurrentHp == 58 && frost.Amount == 8 && frostExecutions.Count == 0,
        "first upgraded Snow Peak frost deals 16 then retains 8 layers");
    target.SetCurrentHpInternal(48); // Second Snow Peak's ordinary 10 damage.
    frost = ApplyNormalFrost(16);
    Check(target.CurrentHp == 24 && frost.Amount == 12 && frostExecutions.Count == 0,
        "two upgraded Snow Peaks do not execute an initially 84-HP target at the pre-decay 24-layer threshold");

    frost.RemoveInternal();
    target.SetCurrentHpInternal(62); // Initially 72 HP, after the first 10 damage.
    frost = ApplyNormalFrost(16);
    target.SetCurrentHpInternal(36); // After the second 10 damage.
    frost = ApplyNormalFrost(16);
    Check(target.CurrentHp == 12 && frost.Amount == 12 && frostExecutions.SequenceEqual([(12, 12m)]),
        "two upgraded Snow Peaks execute at the corrected 72-HP boundary using retained layers");

    frost.RemoveInternal();
    frostExecutions.Clear();
    target.SetCurrentHpInternal(20);
    frost = ApplyNormalFrost(16);
    Check(target.CurrentHp == 4 && frost.Amount == 8 && frostExecutions.SequenceEqual([(4, 8m)]),
        "ordinary application does not execute before its damage segment and decay finish");

    frost.RemoveInternal();
    frostExecutions.Clear();
    target.SetCurrentHpInternal(6);
    frost = FrostbitePower.ApplyFrostAsync(speedContext, target, 6, null, null, triggerFrost: false)
        .GetAwaiter().GetResult()!;
    Check(target.CurrentHp == 6 && frost.Amount == 6 && frostExecutions.SequenceEqual([(6, 6m)]),
        "Deep Winter stack-only application still executes immediately without damage or decay");

    frost.RemoveInternal();
    frostExecutions.Clear();
    target.SetCurrentHpInternal(16);
    var melt = GivePower<WinterMeltPower>(frostOrderPlayer, 1);
    frost = FrostbitePower.ApplyFrostAsync(speedContext, target, 16, target, null).GetAwaiter().GetResult()!;
    Check(target.CurrentHp == 16 && frost.Amount == 16 && frostExecutions.SequenceEqual([(16, 16m)]),
        "Winter Melt still executes immediately at full stacks without a damage segment");
    melt.RemoveInternal();

    frostExecutions.Clear();
    target.SetCurrentHpInternal(16);
    frost.AfterDamageReceived(speedContext, target, null!, default, null, null).GetAwaiter().GetResult();
    Check(frostExecutions.SequenceEqual([(16, 16m)]) && frost.Amount == 16,
        "external damage, including Avalanche before clearing, still checks full remaining layers");

    frost.RemoveInternal();
    frostExecutions.Clear();
    target.SetCurrentHpInternal(100);
    var adept = GivePower<FrozenAdeptPower>(frostOrderPlayer, 1);
    adept.AfterSideTurnEnd(speedContext, MegaCrit.Sts2.Core.Combat.CombatSide.Player, [target]).GetAwaiter().GetResult();
    Check(target.GetPower<FrozenAdeptPower>() == adept && adept.ExtraTriggers == 1,
        "Frost Echo remains active after its owner's turn ends");
    frost = FrostbitePower.ApplyFrostAsync(speedContext, target, 16, target, null).GetAwaiter().GetResult()!;
    Check(target.CurrentHp == 76 && frost.Amount == 4 && frostExecutions.Count == 0,
        "extra frost segments each damage and halve the retained amount");
    frost.RemoveInternal();
    target.SetCurrentHpInternal(100);
    adept.SetAmount(2);
    frost = FrostbitePower.ApplyFrostAsync(speedContext, target, 16, target, null).GetAwaiter().GetResult()!;
    Check(target.CurrentHp == 72 && frost.Amount == 2 && frostExecutions.Count == 0,
        "upgraded Frost Echo resolves 16, 8, 4 damage and retains 2 layers rather than repeating full damage");
    adept.RemoveInternal();

    frost.RemoveInternal();
    target.SetCurrentHpInternal(24);
    doomActionField.SetValue(frostCanonical, (Func<Creature, Task>)(_ =>
        throw new InvalidOperationException("frost-order fault injection")));
    bool executionFailed = false;
    try { ApplyNormalFrost(16); }
    catch (InvalidOperationException exception) when (exception.Message == "frost-order fault injection")
    { executionFailed = true; }
    Check(executionFailed && target.CurrentHp == 8,
        "a terminal execution failure propagates after frost damage and decay");
    frost = target.GetPower<FrostbitePower>()!;
    doomActionField.SetValue(frost, observeFrostExecution);
    frostExecutions.Clear();
    frost.AfterDamageReceived(speedContext, target, null!, default, null, null).GetAwaiter().GetResult();
    Check(frostExecutions.SequenceEqual([(8, 8m)]),
        "execution failure releases both the target deferral and reentrancy guard for later damage");
}
finally
{
    doomActionField.SetValue(frostCanonical, originalDoomAction);
    restoreFrostOrderCombat();
}
// Replay-related post-play exhaustion must not compete with owned pile visuals.
var regroupPlayer = PlayerFixture(1010);
SetField(regroupPlayer, "_runState", MegaCrit.Sts2.Core.Runs.NullRunState.Instance);
SetField(regroupPlayer, "_relics", new List<RelicModel>());
SetField(regroupPlayer, "_potionSlots", new List<PotionModel?>());
SetField(regroupPlayer.PlayerCombatState!, "<OrbQueue>k__BackingField", new MegaCrit.Sts2.Core.Entities.Orbs.OrbQueue(regroupPlayer));
regroupPlayer.ActivateHooks();
var regroupCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
regroupCombat.AddPlayer(regroupPlayer);
ModelDb.Inject(typeof(MegaCrit.Sts2.Core.Models.Monsters.Byrdpip));
var fixtureEnemy = new Creature(ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.Byrdpip>().ToMutable(),
    MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, null);
typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(fixtureEnemy, regroupCombat);
regroupCombat.AddCreature(fixtureEnemy);
var stageCard = ModelDb.Card<ChainClaw>().ToMutable();
regroupCombat.AddCard(stageCard, regroupPlayer);
var stageMethod = typeof(AmbushZone).GetMethod("StageBroughtOutCardAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
var exhaustAfter = typeof(AmbushZone).GetMethod("ExhaustAfterBringOutAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
var restoreRegroupCombat = EnterTestCombat(regroupCombat, true, false);
try
{
    ((Task)stageMethod.Invoke(null, [stageCard])!).GetAwaiter().GetResult();
    Check(stageCard.Pile == regroupPlayer.PlayerCombatState!.PlayPile,
        "a detached brought-out card is staged in Play before AutoPlay's null-pile visual branch");
    Check(regroupPlayer.PlayerCombatState.PlayPile.Cards.Count == 1,
        "staging does not duplicate the card");
    MegaCrit.Sts2.Core.Commands.CardPileCmd.Add(stageCard, PileType.Discard, skipVisuals: true).GetAwaiter().GetResult();
    int pileNotifications = 0;
    regroupPlayer.PlayerCombatState.ExhaustPile.CardAddFinished += () => pileNotifications++;
    Check(((Task<bool>)exhaustAfter.Invoke(null, [speedContext, stageCard, regroupPlayer])!).GetAwaiter().GetResult()
        && stageCard.Pile == regroupPlayer.PlayerCombatState.ExhaustPile,
        "Regroup's post-play path actually exhausts a discarded card");
    Check(pileNotifications == 1, "silent Regroup exhaustion still updates the pile counter once");
    Check(!((Task<bool>)exhaustAfter.Invoke(null, [speedContext, stageCard, regroupPlayer])!).GetAwaiter().GetResult()
        && pileNotifications == 1, "an already exhausted card is neither exhausted nor notified twice");
    var returnedToHand = ModelDb.Card<ChainClaw>().ToMutable();
    regroupCombat.AddCard(returnedToHand, regroupPlayer);
    regroupPlayer.PlayerCombatState.Hand.AddInternal(returnedToHand);
    Check(!((Task<bool>)exhaustAfter.Invoke(null, [speedContext, returnedToHand, regroupPlayer])!).GetAwaiter().GetResult()
        && returnedToHand.Pile == regroupPlayer.PlayerCombatState.Hand,
        "Regroup preserves cards that moved back to hand during their effects");
    Check(!((Task<bool>)exhaustAfter.Invoke(null, [speedContext, returnedToHand, playerTwo])!).GetAwaiter().GetResult(),
        "post-play exhaustion cannot move another player's card");
    var milkEntryPatch = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.WildDogMilkManualEntryPatch")!;
    var milkEntryPrefix = milkEntryPatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
    object?[] ordinaryEntryArgs = [returnedToHand, null];
    Check((bool)milkEntryPrefix.Invoke(null, ordinaryEntryArgs)!,
        "Wild Dog Milk's manual-entry patch leaves ordinary card entry untouched");
    var manualMilk = ModelDb.Card<WildDogMilk>().ToMutable();
    regroupCombat.AddCard(manualMilk, regroupPlayer);
    regroupPlayer.PlayerCombatState.Hand.AddInternal(manualMilk);
    object?[] milkEntryArgs = [manualMilk, null];
    Check(!(bool)milkEntryPrefix.Invoke(null, milkEntryArgs)!,
        "Wild Dog Milk bypasses the initial power-card tween wait");
    ((Task)milkEntryArgs[1]!).GetAwaiter().GetResult();
    Check(manualMilk.Pile == regroupPlayer.PlayerCombatState.PlayPile
        && !regroupPlayer.PlayerCombatState.Hand.Cards.Contains(manualMilk),
        "manual Milk entry really moves Hand to Play instead of only skipping the visual task");
    ModelDb.Inject(typeof(WildDogMilkPower));
    object milkPlay = RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
    typeof(CardPlay).GetProperty("Card")!.SetValue(milkPlay, manualMilk);
    typeof(CardPlay).GetProperty("Player")?.SetValue(milkPlay, regroupPlayer);
    ((Task)typeof(WildDogMilk).GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(manualMilk, [speedContext, milkPlay])!).GetAwaiter().GetResult();
    Check(regroupPlayer.Creature.HasPower<WildDogMilkPower>() && manualMilk.Keywords.Contains(CardKeyword.Eternal),
        "Milk still applies its gameplay power and keeps Eternal after the safe entry path");
    // Run the real death cleanup, without the scene-dependent final game-over screen.
    foreach (Type type in new[] { typeof(FrostThronePower), typeof(ChainClawDiscountPower), typeof(DevourPower),
        typeof(EternalHuntPower), typeof(OverloadPower), typeof(AmbushExhaustionPower) }) ModelDb.Inject(type);
    GivePower<FrostThronePower>(regroupPlayer, 1);
    GivePower<ChainClawDiscountPower>(regroupPlayer, 1);
    GivePower<DevourPower>(regroupPlayer, 1);
    GivePower<EternalHuntPower>(regroupPlayer, 1);
    GivePower<OverloadPower>(regroupPlayer, 1);
    GivePower<AmbushExhaustionPower>(regroupPlayer, 1);
    int deathHandRefreshes = 0;
    returnedToHand.EnergyCostChanged += () => deathHandRefreshes++;
    var killCleanup = typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd).GetMethod("KillWithoutCheckingWinCondition",
        BindingFlags.Static | BindingFlags.NonPublic)!;
    ((Task)killCleanup.Invoke(null, [regroupPlayer.Creature, false, 0])!).GetAwaiter().GetResult();
    Check(regroupPlayer.Creature.IsDead, "real creature death cleanup completes for the custom player");
    Check(!regroupPlayer.Creature.HasPower<WildDogMilkPower>()
        && !regroupPlayer.Creature.HasPower<ChainClawDiscountPower>() && !regroupPlayer.Creature.HasPower<DevourPower>(),
        "death removes custom music, discount and extra-turn powers without interrupting cleanup");
    Check(!regroupPlayer.IsActiveForHooks && deathHandRefreshes == 0,
        "death deactivates the player and never refreshes tearing-down hand card nodes");
    Check(!AmbushZone.For(regroupPlayer).IsCoolingDown,
        "death cleanup clears temporary regional mode flags");


}
finally
{
    restoreRegroupCombat();
}
var musicVolume = typeof(WildDogMilkMusic).GetMethod("CalculateVolume", BindingFlags.Static | BindingFlags.NonPublic)!;
float MusicVolume(float master, float bgm, float track, float fade = 1f) =>
    (float)musicVolume.Invoke(null, [master, bgm, track, fade])!;
Check(MusicVolume(0, 1, 1) == 0 && MusicVolume(1, 0, 1) == 0,
    "either game master mute or BGM mute silences the custom track");
Check(MusicVolume(1, 1, 0) == 0 && MusicVolume(1, 1, 1, 0) == 0,
    "track mute and fade-in start are both silent");
Check(Math.Abs(MusicVolume(0.5f, 0.5f, 0.5f) - 0.03125f) < 0.0001f,
    "custom music follows the same squared master/BGM response as the game");
Check(MusicVolume(1, 1, 2) == 1 && MusicVolume(1, 1, -1) == 0
    && MusicVolume(1, 1, float.NaN) == 0,
    "invalid or out-of-range user volume cannot amplify or corrupt the audio stream");
if (Environment.GetEnvironmentVariable("STS2_NATIVE_BURIAL_TEST") == "1")
{
    // Actual burial paths: the starter relic provides a bounded preparation buffer.
    foreach (Type type in new[] { typeof(FrostFang), typeof(Assassin), typeof(LingdawnpaoSurvival), typeof(Ingenuity), typeof(AmbushCapacityPower), typeof(AmbushZonePower) })
        ModelDb.Inject(type);
    Player BurialPlayer(ulong id)
    {
        var player = PlayerFixture(id);
        SetField(player, "_runState", MegaCrit.Sts2.Core.Runs.NullRunState.Instance);
        SetField(player, "_relics", new List<RelicModel>());
        SetField(player, "_potionSlots", new List<PotionModel?>());
        SetField(player.PlayerCombatState!, "<OrbQueue>k__BackingField", new MegaCrit.Sts2.Core.Entities.Orbs.OrbQueue(player));
        return player;
    }
    RegionalPlanningChecks.Run(Check, BurialPlayer, EnterTestCombat);
    var burialPlayer = BurialPlayer(1012);
    var burialPartner = BurialPlayer(1013);
    var starter = (FrostFang)ModelDb.Relic<FrostFang>().ToMutable();
    starter.Owner = burialPlayer;
    var assassin = (Assassin)ModelDb.Relic<Assassin>().ToMutable();
    assassin.Owner = burialPartner;
    var survival = (LingdawnpaoSurvival)ModelDb.Relic<LingdawnpaoSurvival>().ToMutable();
    survival.Owner = burialPlayer;
    var partnerSurvival = (LingdawnpaoSurvival)ModelDb.Relic<LingdawnpaoSurvival>().ToMutable();
    partnerSurvival.Owner = burialPartner;
    SetField(burialPlayer, "_relics", new List<RelicModel> { starter, survival });
    SetField(burialPartner, "_relics", new List<RelicModel> { assassin, partnerSurvival });
    burialPlayer.ActivateHooks();
    burialPartner.ActivateHooks();
    var burialCombat = new MegaCrit.Sts2.Core.Combat.CombatState();
    burialCombat.AddPlayer(burialPlayer);
    burialCombat.AddPlayer(burialPartner);
    var ongoingEnemy = new Creature(ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.Byrdpip>().ToMutable(),
        MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, null);
    ongoingEnemy.SetMaxHpInternal(200);
    ongoingEnemy.SetCurrentHpInternal(200);
    typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(ongoingEnemy, burialCombat);
    burialCombat.AddCreature(ongoingEnemy);
    var restoreBurialCombat = EnterTestCombat(burialCombat, true, false);
    try
    {
        var zone = AmbushZone.For(burialPlayer);
        GivePower<AmbushCapacityPower>(burialPlayer, 9);
        GivePower<MegaCrit.Sts2.Core.Models.Powers.DexterityPower>(burialPlayer, 10);
        CardModel HandBurialCard(Player player)
        {
            var card = burialCombat.CreateCard<Ingenuity>(player);
            player.PlayerCombatState!.Hand.AddInternal(card);
            return card;
        }
        void Bury(CardModel card, Player player, bool manual = true) =>
            AmbushZone.BuryAsync(speedContext, card, player, spendResources: manual).GetAwaiter().GetResult();

        Bury(HandBurialCard(burialPlayer), burialPlayer, manual: false);
        Check(zone.Count == 1 && burialPlayer.Creature.Block == 0,
            "free effect burial gives no starter Block and does not consume the manual allowance");
        AmbushZone.BuryGeneratedAsync(speedContext, burialPlayer, burialCombat.CreateCard<Ingenuity>(burialPlayer))
            .GetAwaiter().GetResult();
        Check(zone.Count == 2 && burialPlayer.Creature.Block == 0,
            "generated entry gives no starter Block");
        Bury(HandBurialCard(burialPlayer), burialPlayer);
        Check(zone.Count == 3 && burialPlayer.Creature.Block == 3,
            $"first successful manual burial grants exactly three Block despite Dexterity and prior free entries (count={zone.Count}, block={burialPlayer.Creature.Block})");
        Bury(HandBurialCard(burialPlayer), burialPlayer);
        Check(zone.Count == 4 && burialPlayer.Creature.Block == 3,
            "a second manual burial in the same turn grants no additional starter Block");

        survival.BeforeHandDraw(burialPartner, speedContext, burialCombat).GetAwaiter().GetResult();
        Bury(HandBurialCard(burialPlayer), burialPlayer);
        Check(burialPlayer.Creature.Block == 3,
            "another player's draw phase cannot reset this owner's allowance");
        Bury(HandBurialCard(burialPartner), burialPartner);
        Check(burialPartner.Creature.Block == 3 && burialPlayer.Creature.Block == 3,
            "the upgraded-starter owner retains an independent second relic allowance");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        var blocked = HandBurialCard(burialPlayer);
        zone.IsCoolingDown = true;
        Bury(blocked, burialPlayer);
        Check(blocked.Pile?.Type == PileType.Hand && burialPlayer.Creature.Block == 3,
            "a cooldown-rejected burial does not grant Block");
        zone.IsCoolingDown = false;
        Bury(blocked, burialPlayer);
        Check(burialPlayer.Creature.Block == 6,
            "owner's next draw phase refreshes the benefit; rejected burial did not consume it");

        while (!zone.IsFull)
            AmbushZone.BuryGeneratedAsync(speedContext, burialPlayer, burialCombat.CreateCard<Ingenuity>(burialPlayer))
                .GetAwaiter().GetResult();
        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        var overCapacity = HandBurialCard(burialPlayer);
        Bury(overCapacity, burialPlayer);
        Check(overCapacity.Pile?.Type == PileType.Hand && burialPlayer.Creature.Block == 6,
            "full-zone rejection does not grant Block");
        var removed = zone.Cards[^1];
        zone.Pile.RemoveInternal(removed);
        zone.BuriedTurn.Remove(removed);
        burialPlayer.PlayerCombatState!.DiscardPile.AddInternal(removed);
        Bury(overCapacity, burialPlayer);
        Check(burialPlayer.Creature.Block == 9,
            "a valid burial after full-zone rejection still receives the turn's allowance");

        starter.AfterCombatEnd(null!).GetAwaiter().GetResult();
        survival.AfterCombatEnd(null!).GetAwaiter().GetResult();
        starter.BeforeCombatStart().GetAwaiter().GetResult();
        survival.BeforeCombatStart().GetAwaiter().GetResult();
        Bury(HandBurialCard(burialPlayer), burialPlayer);
        Check(zone.Count == 1 && burialPlayer.Creature.Block == 12,
            "combat restart clears a previously consumed allowance");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        var afterDeath = HandBurialCard(burialPlayer);
        burialPlayer.Creature.SetCurrentHpInternal(0);
        Bury(afterDeath, burialPlayer);
        Check(afterDeath.Pile?.Type == PileType.Hand && burialPlayer.Creature.Block == 12,
            "a dead owner's burial cannot grant Block or move the card");
        burialPlayer.Creature.SetCurrentHpInternal(72);
        Bury(afterDeath, burialPlayer);
        Check(burialPlayer.Creature.Block == 15,
            "death-rejected burial does not consume the allowance");

        Check(new Lingdawnpao().StartingRelics.Select(relic => relic.GetType())
                .SequenceEqual(new[] { typeof(FrostFang), typeof(LingdawnpaoSurvival) }),
            "new characters start with both the ambush rules relic and shared survival relic");
        var starterSelection = typeof(MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas)
            .GetMethod("GetStarterRelic", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var touch = new MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas();
        Check(ReferenceEquals(starterSelection.Invoke(touch, [burialPlayer]), starter),
            "native Orobas starter selection still chooses Frost Fang");
        var upgradedStarter = (Assassin)ModelDb.Relic<Assassin>().ToMutable();
        upgradedStarter.Owner = burialPlayer;
        SetField(burialPlayer, "_relics", new List<RelicModel> { upgradedStarter, survival });
        Check(starterSelection.Invoke(touch, [burialPlayer]) == null
                && burialPlayer.GetRelic<LingdawnpaoSurvival>() == survival,
            "after the starter upgrade, the second relic stays and is not selected for a Circlet conversion");

        burialPlayer.Creature.GetPower<AmbushCapacityPower>()!.SetAmount(9);
        Creature FrostTarget()
        {
            var enemy = new Creature(ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.Byrdpip>().ToMutable(),
                MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, null);
            enemy.SetMaxHpInternal(200);
            enemy.SetCurrentHpInternal(200);
            typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(enemy, burialCombat);
            burialCombat.AddCreature(enemy);
            return enemy;
        }
        var firstEnemy = FrostTarget();
        var secondEnemy = FrostTarget();
        FrostbitePower? ApplyEnemyFrost(Creature enemy, Player applier, decimal amount = 1) =>
            MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<FrostbitePower>(speedContext,
                enemy, amount, applier.Creature, null).GetAwaiter().GetResult();

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        ApplyEnemyFrost(firstEnemy, burialPlayer, 5);
        Check(burialPlayer.Creature.Block == 18, "first successful enemy Frostbite application grants three Block");
        Bury(HandBurialCard(burialPlayer), burialPlayer);
        Check(burialPlayer.Creature.Block == 18, "frost first then manual burial shares one allowance");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        Bury(HandBurialCard(burialPlayer), burialPlayer);
        ApplyEnemyFrost(firstEnemy, burialPlayer);
        Check(burialPlayer.Creature.Block == 21, "manual burial first then frost also shares one allowance");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        ApplyEnemyFrost(firstEnemy, burialPlayer);
        ApplyEnemyFrost(secondEnemy, burialPlayer);
        Check(burialPlayer.Creature.Block == 24, "applying frost to multiple enemies cannot multiply the turn's Block");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<FrostbitePower>(speedContext,
            burialPlayer.Creature, 1m, burialPlayer.Creature, null).GetAwaiter().GetResult();
        Check(burialPlayer.Creature.Block == 24, "self-inflicted Frostbite does not grant Block or consume the allowance");
        partnerSurvival.BeforeHandDraw(burialPartner, speedContext, burialCombat).GetAwaiter().GetResult();
        ApplyEnemyFrost(firstEnemy, burialPartner);
        Check(burialPlayer.Creature.Block == 24 && burialPartner.Creature.Block == 6,
            "a teammate's frost grants only their own second relic benefit");
        ApplyEnemyFrost(firstEnemy, burialPlayer);
        Check(burialPlayer.Creature.Block == 27,
            "self-infliction and a teammate's application leave the owner's allowance available");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(speedContext,
            firstEnemy.GetPower<FrostbitePower>()!, -1m, burialPlayer.Creature, null).GetAwaiter().GetResult();
        Check(burialPlayer.Creature.Block == 27, "frost decay does not trigger the relic");
        ApplyEnemyFrost(firstEnemy, burialPlayer);
        Check(burialPlayer.Creature.Block == 30, "successful growth of existing enemy frost triggers the relic");

        survival.BeforeHandDraw(burialPlayer, speedContext, burialCombat).GetAwaiter().GetResult();
        var protectedEnemy = FrostTarget();
        MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>(speedContext,
            protectedEnemy, 1m, null, null).GetAwaiter().GetResult();
        ApplyEnemyFrost(protectedEnemy, burialPlayer, 5);
        Check(!protectedEnemy.HasPower<FrostbitePower>() && burialPlayer.Creature.Block == 30,
            "artifact-blocked frost neither grants Block nor consumes the allowance");
        ApplyEnemyFrost(firstEnemy, burialPlayer);
        Check(burialPlayer.Creature.Block == 33, "a later successful application still receives Block after Artifact rejection");
    }
    finally
    {
        restoreBurialCombat();
    }
}
foreach (Type type in typeof(Lingdawnpao).Assembly.GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(CardModel))))
    ModelDb.Inject(type);
ModelDb.Inject(typeof(LingdawnpaoCardPool));
Check(ModelDb.CardPool<LingdawnpaoCardPool>().AllCards.Count(card => card is LingdawnpaoInitiative) == 1,
    "Seize the Initiative is registered exactly once in the character's normal card pool");
Console.WriteLine($"PASS: {checks} ambush regression checks.");
