using System.Reflection;
using System.Runtime.CompilerServices;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;

internal static class RegionalPlanningChecks
{
    private sealed class Selector : ICardSelector
    {
        internal readonly Queue<Func<CardModel[], int, int, CardModel[]>> Steps = new();
        internal int Calls;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int min, int max)
        {
            Calls++;
            if (Steps.Count == 0) throw new Exception("Unexpected card selector");
            return Task.FromResult<IEnumerable<CardModel>>(Steps.Dequeue()(options.ToArray(), min, max));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new NotSupportedException();
    }

    internal static void Run(Action<bool, string> check, Func<ulong, Player> makePlayer,
        Func<CombatState, bool, bool, Action> enter)
    {
        foreach (var t in new[] { typeof(Regroup), typeof(Concentration), typeof(Ingenuity), typeof(WhetClaw),
            typeof(AmbushStance), typeof(AmbushCapacityPower), typeof(AmbushZonePower), typeof(Byrdpip),
            typeof(HuntOperation), typeof(DamBreak), typeof(DeepWinter), typeof(FrostbitePower),
            typeof(DexterityPower), typeof(LingdawnpaoInitiative), typeof(Expansion),
            typeof(BigSnowballVolley), typeof(BigSnowball), typeof(FrostFang), typeof(Overload),
            typeof(OverloadPower), typeof(AmbushExhaustionPower), typeof(DevourPower),
            typeof(WhetClawPower), typeof(AmbushEchoPower), typeof(AmbushBarrage),
            typeof(ReadyForAnything), typeof(FurGuard), typeof(ClawSwipe), typeof(SnowSift),
            typeof(SneakAttack), typeof(ThroatGrip), typeof(WeakPower), typeof(VulnerablePower),
            typeof(ArtifactPower), typeof(FrostDoubling), typeof(FrostIntensifyPower),
            typeof(FrozenAdeptPower), typeof(TrackedPower), typeof(StrengthPower),
            typeof(FrostStrike), typeof(Snowball), typeof(WinterMeltPower), typeof(CarnivalMomentPower),
            typeof(lingdawnpao.Core.Relics.SnowballPouch), typeof(CloseTheNet), typeof(NightPatrol),
            typeof(NightPatrolPower), typeof(ChainPouncePower), typeof(TigerCombo), typeof(RendPower),
            typeof(ChainClaw), typeof(ChainClawDiscountPower), typeof(SnowstormTorrent), typeof(FrostThronePower), typeof(SnowSweep) }) ModelDb.Inject(t);
        ulong id = 3100;
        void Scenario(Action<Player, CombatState, Selector> run)
        {
            var player = makePlayer(++id);
            typeof(Player).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(player, new Lingdawnpao());
            // Attack damage records statistics here; the uninitialized fixture
            // needs the same default object as the real Player constructor.
            typeof(Player).GetProperty(nameof(Player.ExtraFields))!.SetValue(player,
                Activator.CreateInstance(typeof(Player).GetProperty(nameof(Player.ExtraFields))!.PropertyType));
            var state = new CombatState(); state.AddPlayer(player);
            var enemy = new Creature(ModelDb.Monster<Byrdpip>().ToMutable(), CombatSide.Enemy, null);
            enemy.SetMaxHpInternal(200); enemy.SetCurrentHpInternal(200);
            typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(enemy, state);
            state.AddCreature(enemy); player.ActivateHooks();
            var restore = enter(state, true, false);
            var oldNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            // Beta's native branching play context requires a local identity.
            // Keep this test player remote so the selector never needs a UI room.
            MegaCrit.Sts2.Core.Context.LocalContext.NetId = 0;
            var selector = new Selector();
            try { using var scope = CardSelectCmd.UseSelector(selector); run(player, state, selector); }
            finally { MegaCrit.Sts2.Core.Context.LocalContext.NetId = oldNetId; restore(); }
        }
        var context = new ThrowingPlayerChoiceContext();
        T Add<T>(Player p, CombatState state, PileType pile = PileType.Hand) where T : CardModel, new()
        { var card = state.CreateCard<T>(p); pile.GetPile(p).AddInternal(card); return card; }
        CardPlay Play(CardModel card)
        {
            var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
            typeof(CardPlay).GetProperty(nameof(CardPlay.Card))!.SetValue(play, card);
            typeof(CardPlay).GetProperty(nameof(CardPlay.Target))!.SetValue(play, card.CombatState!.HittableEnemies[0]);
            ((Task)card.GetType().GetMethod("OnPlay", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(card, [context, play])!).GetAwaiter().GetResult();
            return play;
        }
        void Place(Player p, CardModel card) => AmbushZone.BuryAsync(context, card, p, spendResources: false).GetAwaiter().GetResult();

        foreach (bool upgraded in new[] { false, true })
        foreach (bool artifact in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<SnowSweep>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            var first = s.HittableEnemies[0];
            var second = new Creature(ModelDb.Monster<Byrdpip>().ToMutable(), CombatSide.Enemy, null);
            second.SetMaxHpInternal(200); second.SetCurrentHpInternal(200);
            typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(second, s);
            s.AddCreature(second);
            if (artifact) PowerCmd.Apply<ArtifactPower>(context, second, 1m, p.Creature, null).GetAwaiter().GetResult();
            var draws = Enumerable.Range(0, 5).Select(_ => Add<Ingenuity>(p, s, PileType.Draw)).ToArray();
            CardCmd.AutoPlay(context, card, first, skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(first.GetPower<WeakPower>()?.Amount == 2
                && (second.GetPower<WeakPower>()?.Amount ?? 0) == (artifact ? 0 : 2)
                && !second.HasPower<ArtifactPower>() && first.CurrentHp == 200 && second.CurrentHp == 200,
                "Snow Sweep applies two Weak to every opponent independently and respects Artifact without dealing damage");
            check(draws.Count(c => c.Pile?.Type == PileType.Hand) == (upgraded ? 3 : 2)
                && card.Pile?.Type == PileType.Exhaust && sel.Calls == 0,
                "Snow Sweep uses real draw and exhaust commands without a target selector, even when an enemy blocks Weak");
            check(card.Rarity == CardRarity.Rare && card.Type == CardType.Skill && card.EnergyCost.Canonical == 1
                && card.Keywords.Contains(CardKeyword.Exhaust) && card.Keywords.Contains(CardKeyword.Innate) == upgraded,
                "Snow Sweep retains its one-cost Rare skill identity and gains Innate only when upgraded");
        });
        foreach (bool upgraded in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<SnowSweep>(p, s); if (upgraded) card.UpgradeInternal();
            var draws = Enumerable.Range(0, 5).Select(_ => Add<Ingenuity>(p, s, PileType.Draw)).ToArray();
            Place(p, card);
            AmbushZone.BringOutFirstAsync(context, null, p).GetAwaiter().GetResult();
            check(s.HittableEnemies[0].GetPower<WeakPower>()?.Amount == 2
                && draws.Count(c => c.Pile?.Type == PileType.Hand) == (upgraded ? 3 : 2),
                "bringing Snow Sweep out of the zone resolves its group debuff and full draw without a selected enemy");
            check(card.Pile?.Type == PileType.Exhaust && AmbushZone.For(p).Count == 0
                && AmbushZone.For(p).AppliedDex == 0,
                "Snow Sweep exhausts and removes its regional bonus after a native bring-out");
        });

        // Each actual Ambush execution adds a free-use charge. Native AutoPlay
        // and the whole Chain Pounce/Echo sequence must not spend those charges.
        foreach (bool upgraded in new[] { false, true })
        foreach (int echoes in new[] { 0, 1 })
        foreach (bool repeat in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<ChainClaw>(p, s); if (upgraded) card.UpgradeInternal(); Place(p, card);
            AmbushZone.For(p).BuriedTurn[card] = -1;
            if (echoes > 0) PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, echoes, p.Creature, null).GetAwaiter().GetResult();
            if (repeat) PowerCmd.Apply<ChainPouncePower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            int expected = (1 + echoes) * (repeat ? 2 : 1);
            var power = p.Creature.GetPower<ChainClawDiscountPower>()!;
            check(power.Amount == expected && AmbushZone.For(p).Effects.Count == 1
                && card.Rarity == CardRarity.Uncommon && card.DynamicVars.Damage.BaseValue == (upgraded ? 8 : 6),
                "Chain Claw promotes to Uncommon without damage changes and stacks one free use per Ambush execution");
            var attack = Add<WhetClaw>(p, s);
            var auto = Add<ClawSwipe>(p, s);
            CardCmd.AutoPlay(context, auto, s.HittableEnemies[0], skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(power.Amount == expected && attack.EnergyCost.GetAmountToSpend() == 0,
                "native automatic attacks preserve every free use while a manual two-cost attack becomes free");
            var xAttack = Add<SnowstormTorrent>(p, s);
            var xPlay = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
            typeof(CardPlay).GetProperty(nameof(CardPlay.Card))!.SetValue(xPlay, xAttack);
            power.BeforeCardPlayed(xPlay).GetAwaiter().GetResult();
            check(!power.TryModifyEnergyCostInCombat(xAttack, 3m, out var _) && power.Amount == expected,
                "X attacks keep their existing cost rules and do not consume a fixed-cost free use");
        });
        foreach (int echoes in new[] { 0, 1 })
        foreach (bool whet in new[] { false, true }) Scenario((p, s, sel) =>
        {
            AmbushZone.For(p).Effects.Record(new(AmbushEffectKind.ChainClaw, 1m));
            if (whet) AmbushZone.For(p).Effects.Record(new(AmbushEffectKind.WhetClaw));
            if (echoes > 0) PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, echoes, p.Creature, null).GetAwaiter().GetResult();
            var card = Add<AmbushBarrage>(p, s); Place(p, card); AmbushZone.For(p).BuriedTurn[card] = -1;
            AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            int expected = (1 + echoes) * (whet ? 2 : 1);
            var power = p.Creature.GetPower<ChainClawDiscountPower>()!;
            check(power.Amount == expected,
                "Barrage inherits free-use charges with Echo/Whet repetition and does not consume its own newly granted uses");
            var attack = Add<WhetClaw>(p, s); int energy = p.PlayerCombatState!.Energy;
            for (int i = expected; i > 0; i--)
            {
                var spent = attack.SpendResources().GetAwaiter().GetResult();
                power.BeforeCardPlayed(PlayFixture(attack)).GetAwaiter().GetResult();
                check(spent.Item1 == 0 && p.PlayerCombatState.Energy == energy
                    && (p.Creature.GetPower<ChainClawDiscountPower>()?.Amount ?? 0) == i - 1,
                    "each manual attack spends zero actual energy and consumes exactly one accumulated free use");
            }
            check(attack.EnergyCost.GetAmountToSpend() == 2,
                "normal cost returns immediately after the last accumulated free use");
        });
        CardPlay PlayFixture(CardModel card)
        {
            var play = (CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
            typeof(CardPlay).GetProperty(nameof(CardPlay.Card))!.SetValue(play, card);
            return play;
        }
        Scenario((p, s, sel) =>
        {
            var power = PowerCmd.Apply<ChainClawDiscountPower>(context, p.Creature, 2m, p.Creature, null).GetAwaiter().GetResult()!;
            var first = Add<WhetClaw>(p, s); var second = Add<SnowPeakCrush>(p, s);
            int energy = p.PlayerCombatState!.Energy;
            AmbushZone.BuryAsync(context, first, p, spendResources: true).GetAwaiter().GetResult();
            check(power.Amount == 1 && p.PlayerCombatState.Energy == energy && AmbushZone.For(p).Cards.Contains(first),
                "manual burial uses the first free charge rather than all accumulated charges");
            AmbushZone.BuryAsync(context, second, p, spendResources: true).GetAwaiter().GetResult();
            check(!p.Creature.HasPower<ChainClawDiscountPower>() && p.PlayerCombatState.Energy == energy
                && AmbushZone.For(p).Cards.Contains(second) && Add<WhetClaw>(p, s).EnergyCost.GetAmountToSpend() == 2,
                "manual burial spends the final free charge and cannot repeatedly reuse it");
        });
        Scenario((p, s, sel) =>
        {
            var power = PowerCmd.Apply<ChainClawDiscountPower>(context, p.Creature, 3m, p.Creature, null).GetAwaiter().GetResult()!;
            var freeBury = Add<ClawSwipe>(p, s); Place(p, freeBury);
            check(power.Amount == 3, "effect-driven free burial does not spend manual free-attack uses");
            var attack = Add<WhetClaw>(p, s);
            power.AfterSideTurnEnd(context, CombatSide.Enemy, s.HittableEnemies).GetAwaiter().GetResult();
            check(power.Amount == 3 && p.Creature.HasPower<ChainClawDiscountPower>(),
                "another side's turn-end callback cannot remove the owner's active-turn uses");
            power.AfterSideTurnEnd(context, CombatSide.Player, new[] { p.Creature }).GetAwaiter().GetResult();
            p.PlayerCombatState!.IncrementTurnNumber();
            check(!p.Creature.HasPower<ChainClawDiscountPower>() && attack.EnergyCost.GetAmountToSpend() == 2,
                "all unused charges expire at the owner's turn end before the next or an extra turn");
        });
        foreach (bool burial in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var throne = PowerCmd.Apply<FrostThronePower>(context, p.Creature, 3m, p.Creature, null).GetAwaiter().GetResult()!;
            var attack = Add<WhetClaw>(p, s);
            check(attack.EnergyCost.GetAmountToSpend() == 0,
                "an energy-starved preview records Frost Throne's pending loan before acquiring the free buff");
            PowerCmd.Apply<ChainClawDiscountPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            if (burial) AmbushZone.BuryAsync(context, attack, p, spendResources: true).GetAwaiter().GetResult();
            else
            {
                attack.SpendResources().GetAwaiter().GetResult();
                var play = PlayFixture(attack);
                p.Creature.GetPower<ChainClawDiscountPower>()!.BeforeCardPlayed(play).GetAwaiter().GetResult();
                throne.AfterCardPlayed(context, play).GetAwaiter().GetResult();
            }
            check(!p.Creature.HasPower<FrostbitePower>()
                && !(bool)typeof(FrostThronePower).GetField("_loanUsedThisTurn", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(throne)!,
                "a real free attack or burial cancels the old loan preview without self-frost or consuming Frost Throne");
        });

        // An actual generated token, rather than a second direct ice application:
        // Strength affects both attacks, Melt suppresses only the token's ice damage.
        foreach (bool upgraded in new[] { false, true })
        foreach (int strength in new[] { 0, 2 })
        foreach (bool melt in new[] { false, true }) Scenario((p, s, sel) =>
        {
            if (strength > 0) PowerCmd.Apply<StrengthPower>(context, p.Creature, strength, p.Creature, null).GetAwaiter().GetResult();
            if (melt) PowerCmd.Apply<WinterMeltPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            var card = Add<FrostStrike>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            CardCmd.AutoPlay(context, card, s.HittableEnemies[0], skipCardPileVisuals: true).GetAwaiter().GetResult();
            var token = p.PlayerCombatState!.Hand.Cards.OfType<Snowball>().Single();
            int body = (upgraded ? 9 : 7) + strength, frost = upgraded ? 3 : 2;
            check(token.IsUpgraded == upgraded && s.HittableEnemies[0].CurrentHp == 200 - body
                && !s.HittableEnemies[0].HasPower<FrostbitePower>(),
                "Frost Strike generates exactly one correctly upgraded Snowball without applying ice itself");
            CardCmd.AutoPlay(context, token, s.HittableEnemies[0], skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(s.HittableEnemies[0].CurrentHp == 200 - body - 1 - strength - (melt ? 0 : frost)
                && s.HittableEnemies[0].GetPower<FrostbitePower>()!.Amount == (melt ? frost : (frost + 1) / 2)
                && token.Pile?.Type == PileType.Exhaust,
                $"Frost Strike token resolution (up={upgraded}, str={strength}, melt={melt}, HP={s.HittableEnemies[0].CurrentHp}, ice={s.HittableEnemies[0].GetPower<FrostbitePower>()?.Amount}, pile={token.Pile?.Type})");
        });
        foreach (bool pouch in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var other = new Creature(ModelDb.Monster<Byrdpip>().ToMutable(), CombatSide.Enemy, null);
            other.SetMaxHpInternal(200); other.SetCurrentHpInternal(200);
            typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(other, s); s.AddCreature(other);
            PowerCmd.Apply<CarnivalMomentPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            if (pouch)
            {
                var relic = ModelDb.Relic<lingdawnpao.Core.Relics.SnowballPouch>().ToMutable(); relic.Owner = p;
                typeof(Player).GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(p, new List<RelicModel> { relic });
            }
            Play(Add<FrostStrike>(p, s));
            var token = p.PlayerCombatState!.Hand.Cards.OfType<Snowball>().Single(); Play(token);
            int ice = pouch ? 5 : 3;
            check(s.HittableEnemies[0].CurrentHp == 192 - ice && other.CurrentHp == 200 - ice,
                "Frost Strike's Snowball receives Carnival and Pouch bonuses; only its ice splashes to the second enemy");
        });

        // Close the Net pays Block before selection while all regional Dexterity
        // is still present. A chosen middle card must not also release the FIFO.
        foreach (bool upgraded in new[] { false, true })
        foreach (bool mature in new[] { false, true }) Scenario((p, s, sel) =>
        {
            PowerCmd.Apply<DexterityPower>(context, p.Creature, 2m, p.Creature, null).GetAwaiter().GetResult();
            var first = Add<ClawSwipe>(p, s); var chosen = Add<Ingenuity>(p, s); var last = Add<DeepWinter>(p, s);
            Place(p, first); Place(p, chosen); Place(p, last);
            if (mature) AmbushZone.For(p).BuriedTurn[chosen] = -1;
            var card = Add<CloseTheNet>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            int initial = (upgraded ? 30 : 24) + 4;
            sel.Steps.Enqueue((options, min, max) => {
                check(p.Creature.Block == initial && options.SequenceEqual(new CardModel[] { first, chosen, last }),
                    "Close the Net grants all up-front Block before presenting the regional selection");
                return [chosen];
            });
            var play = Play(card);
            var starter = (FrostFang)ModelDb.Relic<FrostFang>().ToMutable(); starter.Owner = p;
            starter.AfterCardPlayed(context, play).GetAwaiter().GetResult();
            check(AmbushZone.For(p).Cards.SequenceEqual(new CardModel[] { first, last }) && chosen.Pile?.Type == PileType.Discard
                && p.Creature.Block == initial + 8 + (mature ? 8 : 0),
                "Close the Net releases only the chosen card, preserves order, and does not unlock fresh Ambush bonuses");
        });
        foreach (bool upgraded in new[] { false, true })
        foreach (bool disabled in new[] { false, true }) Scenario((p, s, sel) =>
        {
            if (disabled) Place(p, Add<DeepWinter>(p, s));
            AmbushZone.For(p).IsCoolingDown = disabled;
            AmbushZone.RecomputeBonusesAsync(context, p).GetAwaiter().GetResult();
            var card = Add<CloseTheNet>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            CardCmd.AutoPlay(context, card, s.HittableEnemies[0], skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(sel.Calls == 0 && p.Creature.Block == (upgraded ? 30 : 24)
                && card.Type == CardType.Skill && card.Pile?.Type == PileType.Exhaust
                && card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
                && AmbushZone.For(p).Count == (disabled ? 1 : 0),
                "empty or disabled Close the Net still grants full Block and exhausts at two cost without a selector");
        });

        foreach (bool upgraded in new[] { false, true })
        foreach (bool skill in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<NightPatrol>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            CardModel stored = skill ? Add<Ingenuity>(p, s) : Add<ClawSwipe>(p, s); Place(p, stored);
            Play(card); int amount = upgraded ? 2 : 1;
            var power = p.Creature.GetPower<NightPatrolPower>()!;
            power.AfterSideTurnStart(CombatSide.Player, new[] { p.Creature }, s).GetAwaiter().GetResult();
            check((p.Creature.GetPower<StrengthPower>()?.Amount ?? 0) == (skill ? 0 : 1)
                && (p.Creature.GetPower<DexterityPower>()?.Amount ?? 0) == (skill ? 1 : 0),
                "Dark Harvest no longer grows merely for keeping a card across a turn start");
            AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            check((skill ? p.Creature.Block == 5 : s.HittableEnemies[0].CurrentHp == 193)
                && p.Creature.GetPower<StrengthPower>()!.Amount == amount
                && p.Creature.GetPower<DexterityPower>()!.Amount == amount,
                "Dark Harvest grows after completion, without retroactive damage or Block on the first card");
            Place(p, Add<ClawSwipe>(p, s)); AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            check(p.Creature.GetPower<StrengthPower>()!.Amount == amount && p.Creature.GetPower<DexterityPower>()!.Amount == amount,
                "multiple same-turn bring-outs cannot farm Dark Harvest growth");
            p.PlayerCombatState!.IncrementTurnNumber();
            Place(p, Add<ClawSwipe>(p, s)); AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            check(p.Creature.GetPower<StrengthPower>()!.Amount == 2 * amount && p.Creature.GetPower<DexterityPower>()!.Amount == 2 * amount,
                "a new player turn including an extra turn permits the next Dark Harvest growth");
        });
        foreach (bool upgraded in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var starter = (FrostFang)ModelDb.Relic<FrostFang>().ToMutable(); starter.Owner = p;
            Place(p, Add<ClawSwipe>(p, s));
            var card = Add<NightPatrol>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            var play = Play(card); starter.AfterCardPlayed(context, play).GetAwaiter().GetResult();
            check(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2
                && AmbushZone.For(p).Count == 0 && p.Creature.GetPower<StrengthPower>()!.Amount == (upgraded ? 2 : 1)
                && s.HittableEnemies[0].CurrentHp == 193,
                "manually playing two-cost Dark Harvest can start growth through the starter relic in the same turn");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<NightPatrol>(p, s); Place(p, card);
            AmbushZone.BringOutFirstAsync(context, null, p).GetAwaiter().GetResult();
            check(p.Creature.GetPower<NightPatrolPower>() != null && p.Creature.GetPower<StrengthPower>()!.Amount == 1
                && p.Creature.GetPower<DexterityPower>()!.Amount == 1,
                "a brought-out Dark Harvest becomes active before its own bring-out completes");
        });
        foreach (bool nested in new[] { false, true }) Scenario((p, s, sel) =>
        {
            PowerCmd.Apply<NightPatrolPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            if (nested)
            {
                Place(p, Add<TigerCombo>(p, s)); Place(p, Add<ClawSwipe>(p, s)); Place(p, Add<DeepWinter>(p, s));
            }
            else
            {
                Place(p, Add<Ingenuity>(p, s));
                PowerCmd.Apply<ChainPouncePower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
                PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
                AmbushZone.For(p).BuriedTurn[AmbushZone.For(p).Cards[0]] = -1;
            }
            AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            check(AmbushZone.For(p).Count == 0 && p.Creature.GetPower<StrengthPower>()!.Amount == 1
                && p.Creature.GetPower<DexterityPower>()!.Amount == 1,
                "nested Tiger Combo or Chain Pounce plus Echo still grants Dark Harvest growth exactly once");
        });
        Scenario((p, s, sel) =>
        {
            PowerCmd.Apply<NightPatrolPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            var other = makePlayer(++id); s.AddPlayer(other);
            NightPatrolPower.NotifyBroughtOutAsync(context, other).GetAwaiter().GetResult();
            Place(p, Add<Ingenuity>(p, s)); AmbushZone.For(p).IsCoolingDown = true;
            var result = AmbushZone.BringOutFirstAsync(context, null, p).GetAwaiter().GetResult();
            check(result == null && !p.Creature.HasPower<StrengthPower>(),
                "a teammate's bring-out or a disabled zone does not trigger the owner's Dark Harvest");
            AmbushZone.For(p).IsCoolingDown = false;
            AmbushZone.BringOutFirstAsync(context, null, p).GetAwaiter().GetResult();
            check(p.Creature.GetPower<StrengthPower>()!.Amount == 1,
                "failed or foreign bring-outs do not consume the owner's first successful growth");
        });

        // Doubling stores existing frost exactly: it is not another application
        // with damage, decay, snowball bonuses or extra Frost Echo triggers.
        foreach (bool upgraded in new[] { false, true })
        foreach (int layers in new[] { 1, 5, 10 }) Scenario((p, s, sel) =>
        {
            var target = s.HittableEnemies[0];
            var other = new Creature(ModelDb.Monster<Byrdpip>().ToMutable(), CombatSide.Enemy, null);
            other.SetMaxHpInternal(200); other.SetCurrentHpInternal(200);
            typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(other, s); s.AddCreature(other);
            var frost = PowerCmd.Apply<FrostbitePower>(context, target, layers, p.Creature, null).GetAwaiter().GetResult()!;
            PowerCmd.Apply<FrozenAdeptPower>(context, p.Creature, 2m, p.Creature, null).GetAwaiter().GetResult();
            var intensify = PowerCmd.Apply<FrostIntensifyPower>(context, p.Creature, 3m, p.Creature, null).GetAwaiter().GetResult()!;
            var card = Add<FrostDoubling>(p, s); if (upgraded) card.UpgradeInternal();
            Play(card);
            check(target.CurrentHp == 200 && frost.Amount == 2 * layers && intensify.Amount == 3,
                "Freeze Over doubles existing layers exactly without damage, decay, Frost Echo or consuming Intensify");
            check(!other.HasPower<FrostbitePower>() && other.CurrentHp == 200
                && card.Keywords.Contains(CardKeyword.Retain) && card.Keywords.Contains(CardKeyword.Exhaust),
                "Freeze Over leaves unfrozen enemies unchanged and retains its Retain/Exhaust identity");
        });
        foreach (bool upgraded in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var target = s.HittableEnemies[0];
            var frost = PowerCmd.Apply<FrostbitePower>(context, target, 5m, p.Creature, null).GetAwaiter().GetResult()!;
            PowerCmd.Apply<FrozenAdeptPower>(context, p.Creature, 2m, p.Creature, null).GetAwaiter().GetResult();
            var card = Add<DeepWinter>(p, s); if (upgraded) card.UpgradeInternal();
            Play(card);
            check(target.CurrentHp == 200 && frost.Amount == (upgraded ? 14 : 11),
                "Deep Winter adds six/nine layers without damage or decay even with additional Frost Echo triggers");
        });
        foreach (bool doubling in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var target = s.HittableEnemies[0];
            var frost = PowerCmd.Apply<FrostbitePower>(context, target, 3m, p.Creature, null).GetAwaiter().GetResult()!;
            target.SetCurrentHpInternal(6);
            int executions = 0;
            typeof(FrostbitePower).GetField("_doomAction", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(frost, (Func<Creature, Task>)(_ => { executions++; return Task.CompletedTask; }));
            CardModel card = doubling ? Add<FrostDoubling>(p, s) : Add<DeepWinter>(p, s);
            Play(card);
            check(executions == 1 && frost.Amount == (doubling ? 6 : 9) && target.CurrentHp == 6,
                "stack-only Freeze Over and Deep Winter still execute once at the threshold without a damage/decay segment");
        });

        // Install only the production damage-dispatch patch. The harness does
        // not otherwise load the mod's UI/animation Harmony patches.
        var damagePatch = typeof(TrackedPower).Assembly.GetTypes()
            .Single(t => t.Name == "AbstractModel_ModifyDamageMultiplicative_PowerMultiplierPatch");
        var trackingHarmony = new HarmonyLib.Harmony("snowleopard.tests.tracked-damage");
        trackingHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(AbstractModel), "ModifyDamageMultiplicative"),
            postfix: new HarmonyLib.HarmonyMethod(damagePatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!));
        try
        {
            foreach (bool vulnerable in new[] { false, true }) Scenario((p, s, sel) =>
            {
                var first = s.HittableEnemies[0];
                var tracked = new Creature(ModelDb.Monster<Byrdpip>().ToMutable(), CombatSide.Enemy, null);
                tracked.SetMaxHpInternal(200); tracked.SetCurrentHpInternal(200);
                typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(tracked, s); s.AddCreature(tracked);
                var hunt = PowerCmd.Apply<TrackedPower>(context, tracked, 2m, p.Creature, null).GetAwaiter().GetResult()!;
                PowerCmd.Apply<StrengthPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
                if (vulnerable) PowerCmd.Apply<VulnerablePower>(context, tracked, 1m, p.Creature, null).GetAwaiter().GetResult();
                var card = Add<ClawSwipe>(p, s); Place(p, card);
                // 6 printed + 1 permanent Strength + 1 regional Strength, then
                // the real production 1.5 multiplier (and native Vulnerable).
                AmbushZone.BringOutFirstAsync(context, first, p).GetAwaiter().GetResult();
                check(first.CurrentHp == 200 && tracked.CurrentHp == (vulnerable ? 182 : 188),
                    "Hunted redirects a fresh regional attack and increases its post-Strength damage by 50%, stacking with Vulnerable");
                hunt.AfterSideTurnEnd(context, CombatSide.Enemy, s.HittableEnemies).GetAwaiter().GetResult();
                check(hunt.Amount == 1, "Hunted's damage buff preserves its two-turn duration rather than scaling damage with stacks");
            });
            Scenario((p, s, sel) =>
            {
                var target = s.HittableEnemies[0];
                PowerCmd.Apply<TrackedPower>(context, target, 2m, p.Creature, null).GetAwaiter().GetResult();
                PowerCmd.Apply<StrengthPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
                Play(Add<ClawSwipe>(p, s));
                check(target.CurrentHp == 193, "Hunted does not amplify ordinary attacks played directly from hand");
                FrostbitePower.ApplyFrostAsync(context, target, 4m, p.Creature, null).GetAwaiter().GetResult();
                check(target.CurrentHp == 189, "Hunted does not amplify unpowered frost damage");
            });
        }
        finally { trackingHarmony.UnpatchAll("snowleopard.tests.tracked-damage"); }

        // Exercise the real manual-play resource path, then change the inventory again.
        // Attack inventory must discount a defensive skill without becoming Block.
        foreach (bool upgraded in new[] { false, true })
        foreach (bool skills in new[] { false, true })
        foreach (int count in Enumerable.Range(0, 6)) Scenario((p, s, sel) =>
        {
            PowerCmd.Apply<AmbushCapacityPower>(context, p.Creature, 5m, p.Creature, null).GetAwaiter().GetResult();
            var card = Add<ReadyForAnything>(p, s); if (upgraded) card.UpgradeInternal();
            var observer = Add<ReadyForAnything>(p, s);
            for (int i = 0; i < count; i++)
                Place(p, skills ? Add<FurGuard>(p, s) : (CardModel)Add<ClawSwipe>(p, s));
            int expectedCost = Math.Max(0, 4 - count);
            ReadyForAnything.SyncCosts(p); ReadyForAnything.SyncCosts(p);
            check(card.EnergyCost.GetAmountToSpend() == expectedCost
                && observer.EnergyCost.GetAmountToSpend() == expectedCost,
                "Ready for Anything updates every copy for 0-5 stored cards and repeated synchronization never stacks the discount");
            p.PlayerCombatState!.GainEnergy(4);
            check(card.CanPlay() && card.IsValidTarget(null) && card.Type == CardType.Skill && card.GainsBlock,
                "Ready for Anything is a playable self-targeting defensive skill without an enemy target");
            var spent = card.SpendResources().GetAwaiter().GetResult();
            card.OnPlayWrapper(context, null, false, new ResourceInfo {
                EnergySpent = spent.Item1, EnergyValue = spent.Item1,
                StarsSpent = spent.Item2, StarValue = spent.Item2 }, skipCardPileVisuals: true).GetAwaiter().GetResult();
            int expectedBlock = (upgraded ? 21 : 16) + (skills ? count : 0);
            check(p.Creature.Block == expectedBlock && s.HittableEnemies[0].CurrentHp == 200
                && p.PlayerCombatState.Energy == 4 - expectedCost && card.Pile?.Type == PileType.Discard,
                "Ready for Anything pays its current discount, gains 16/21 Block plus Dexterity, deals no damage and remains reusable");
            if (count > 0)
            {
                AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
                check(observer.EnergyCost.GetAmountToSpend() == Math.Max(0, 5 - count)
                    && AmbushZone.For(p).Count == count - 1
                    && p.Creature.Block == expectedBlock + (skills ? 5 + count : 0)
                    && s.HittableEnemies[0].CurrentHp == 200 - (skills ? 0 : 6 + count),
                    "bringing a stored card out restores the defensive card's cost and preserves normal attack/skill resolution");
            }
        });
        Scenario((p, s, sel) =>
        {
            var starter = (FrostFang)ModelDb.Relic<FrostFang>().ToMutable(); starter.Owner = p;
            typeof(Player).GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(p, new List<RelicModel> { starter });
            var card = Add<ReadyForAnything>(p, s); var observer = Add<ReadyForAnything>(p, s);
            p.PlayerCombatState!.GainEnergy(3);
            for (int i = 0; i < 3; i++)
                AmbushZone.BuryAsync(context, Add<FurGuard>(p, s), p).GetAwaiter().GetResult();
            check(p.PlayerCombatState.Energy == 0 && card.EnergyCost.GetAmountToSpend() == 1 && !card.CanPlay(),
                "three paid burials cannot fund a fourth-cost Ready for Anything in a three-energy turn");
            p.PlayerCombatState.GainEnergy(1);
            var spent = card.SpendResources().GetAwaiter().GetResult();
            card.OnPlayWrapper(context, null, false, new ResourceInfo {
                EnergySpent = spent.Item1, EnergyValue = spent.Item1,
                StarsSpent = spent.Item2, StarValue = spent.Item2 }, skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(p.Creature.Block == 27 && AmbushZone.For(p).Count == 2
                && AmbushZone.For(p).AppliedDex == 2 && observer.EnergyCost.GetAmountToSpend() == 2
                && p.PlayerCombatState.Energy == 0 && s.HittableEnemies[0].CurrentHp == 200,
                "a funded defensive play brings exactly one card out through the starter, then restores costs and Dexterity");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<ReadyForAnything>(p, s); Place(p, card);
            check(AmbushZone.For(p).AppliedDex == 1 && AmbushZone.For(p).AppliedStr == 0,
                "storing the converted Ready for Anything grants Dexterity rather than Strength");
            AmbushZone.BringOutFirstAsync(context, null, p).GetAwaiter().GetResult();
            check(p.Creature.Block == 17 && AmbushZone.For(p).Count == 0
                && card.Pile?.Type == PileType.Discard && s.HittableEnemies[0].CurrentHp == 200,
                "bringing Ready for Anything out grants Block without damage or a required target and cleans up its regional bonus");
        });

        foreach (var (count, upgraded) in new[] { (0, false), (2, false), (2, true) }) Scenario((p, s, sel) =>
        {
            var card = Add<Overload>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            var zone = AmbushZone.For(p);
            if (count > 0) { Place(p, Add<WhetClaw>(p, s)); Place(p, Add<DeepWinter>(p, s)); }
            var stored = zone.Cards.ToArray();
            var draws = Enumerable.Range(0, 5).Select(_ => Add<Ingenuity>(p, s, PileType.Draw)).ToArray();
            CardCmd.AutoPlay(context, card, p.Creature, skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(p.PlayerCombatState!.Energy == count && draws.Count(c => c.Pile?.Type == PileType.Hand) == count,
                "Overload actually grants energy and draws cards equal to the regional count, including an empty region");
            check(card.Pile?.Type == PileType.Exhaust && zone.Cards.SequenceEqual(stored)
                && !zone.IsCoolingDown && zone.AppliedStr == count / 2 && zone.AppliedDex == count / 2,
                "Overload exhausts without sweeping the zone or changing its current-turn stats");
            var pending = p.Creature.GetPower<OverloadPower>()!;
            pending.BeforeSideTurnStart(context, CombatSide.Player, [p.Creature], s).GetAwaiter().GetResult();
            p.PlayerCombatState.IncrementTurnNumber();
            pending.BeforeSideTurnStart(context, CombatSide.Enemy, [s.HittableEnemies[0]], s).GetAwaiter().GetResult();
            check(!zone.IsCoolingDown && p.Creature.HasPower<OverloadPower>(),
                "the current turn and an enemy's turn cannot activate the pending Overload penalty");
            pending.BeforeSideTurnStart(context, CombatSide.Player, [p.Creature], s).GetAwaiter().GetResult();
            check(zone.IsCoolingDown && zone.AppliedStr == 0 && zone.AppliedDex == 0
                && !p.Creature.HasPower<OverloadPower>() && zone.Cards.SequenceEqual(stored),
                "the owner's next turn disables regional stats before draw and keeps every stored card");
            var keep = Add<WhetClaw>(p, s); Place(p, keep);
            var brought = AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            check(keep.Pile?.Type == PileType.Hand && brought == null && zone.Cards.SequenceEqual(stored),
                "real burial and bring-out both stop during Overload exhaustion");
            zone.IsCoolingDown = false;
            check(zone.IsCoolingDown, "another effect clearing ordinary cooldown cannot bypass Overload exhaustion");
            p.PlayerCombatState.IncrementTurnNumber();
            p.Creature.GetPower<AmbushExhaustionPower>()!.BeforeSideTurnStart(context, CombatSide.Player, [p.Creature], s)
                .GetAwaiter().GetResult();
            check(!zone.IsCoolingDown && zone.AppliedStr == count / 2 && zone.AppliedDex == count / 2
                && zone.Cards.SequenceEqual(stored), "the following owner turn restores regional stats without moving stored cards");
        });
        foreach (bool exhaustionEndsFirst in new[] { false, true }) Scenario((p, s, sel) =>
        {
            Place(p, Add<DeepWinter>(p, s));
            var exhaustion = PowerCmd.Apply<AmbushExhaustionPower>(context, p.Creature, 1m, p.Creature, null)
                .GetAwaiter().GetResult()!;
            var speed = PowerCmd.Apply<DevourPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult()!;
            speed.AfterTakingExtraTurn(p).GetAwaiter().GetResult();
            void EndExhaustion()
            {
                p.PlayerCombatState!.IncrementTurnNumber();
                exhaustion.BeforeSideTurnStart(context, CombatSide.Player, [p.Creature], s).GetAwaiter().GetResult();
            }
            void EndSpeed() => speed.AfterSideTurnEnd(context, CombatSide.Player, [p.Creature]).GetAwaiter().GetResult();
            if (exhaustionEndsFirst) EndExhaustion(); else EndSpeed();
            check(AmbushZone.For(p).IsCoolingDown && AmbushZone.For(p).AppliedDex == 0,
                "ending either Godspeed or Overload exhaustion alone leaves the other regional penalty active");
            if (exhaustionEndsFirst) EndSpeed(); else EndExhaustion();
            check(!AmbushZone.For(p).IsCoolingDown && AmbushZone.For(p).AppliedDex == 1,
                "regional Dexterity returns only after both Godspeed and Overload exhaustion have ended");
        });

        // Skill conversion must remove the full-zone gate without gaining the
        // starter relic's first-Power expansion or leaving a reusable source.
        foreach (bool upgraded in new[] { false, true })
        foreach (bool full in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var starter = (FrostFang)ModelDb.Relic<FrostFang>().ToMutable(); starter.Owner = p;
            typeof(Player).GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(p, new List<RelicModel> { starter });
            PowerCmd.Apply<AmbushCapacityPower>(context, p.Creature, AmbushZone.MaxSize, p.Creature, null)
                .GetAwaiter().GetResult();
            if (full) while (!AmbushZone.For(p).IsFull) Place(p, Add<Ingenuity>(p, s));
            int capacity = AmbushZone.For(p).Capacity;
            var card = Add<Expansion>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            CardCmd.AutoPlay(context, card, p.Creature, skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(AmbushZone.For(p).Capacity == capacity + (upgraded ? 2 : 1)
                && !AmbushZone.For(p).PowerCapacityUsedThisTurn,
                "Expansion works in an empty or full zone and does not grant or consume the first-Power expansion");
            check(card.Pile?.Type == PileType.Exhaust && card.Type == CardType.Skill,
                "Expansion is a skill and actually exhausts after native play");
        });
        Scenario((p, s, sel) =>
        {
            PowerCmd.Apply<AmbushCapacityPower>(context, p.Creature, AmbushZone.CapacityHardCap - 1, p.Creature, null)
                .GetAwaiter().GetResult();
            var card = Add<Expansion>(p, s, PileType.Play); card.UpgradeInternal(); Play(card);
            check(AmbushZone.For(p).Capacity == AmbushZone.CapacityHardCap
                && p.Creature.GetPower<AmbushCapacityPower>()!.Amount == AmbushZone.CapacityHardCap,
                "upgraded Expansion clamps its two-slot increase at the regional hard cap");
        });
        foreach (bool upgraded in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<BigSnowballVolley>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            CardCmd.AutoPlay(context, card, p.Creature, skipCardPileVisuals: true).GetAwaiter().GetResult();
            var snowballs = p.PlayerCombatState!.Hand.Cards.OfType<BigSnowball>().ToArray();
            check(snowballs.Length == 2 && snowballs.All(c => c.IsUpgraded == upgraded
                && c.DynamicVars["FrostbitePower"].BaseValue == (upgraded ? 5 : 4)),
                "Big Snowball Volley generates exactly two snowballs of the correct upgrade quality");
            check(card.Pile?.Type == PileType.Discard && card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1,
                "Big Snowball Volley remains reusable at one cost before and after upgrading");
        });

        Scenario((p, s, sel) =>
        {
            var card = Add<LingdawnpaoInitiative>(p, s, PileType.Play);
            var chosen = Add<WhetClaw>(p, s); var spare = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => {
                check(min == 1 && max == 1 && options.Contains(chosen), "base Initiative permits choosing an unaffordable hand card");
                return [chosen];
            });
            CardCmd.AutoPlay(context, card, p.Creature, skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(AmbushZone.For(p).Cards.SequenceEqual(new[] { chosen }) && p.PlayerCombatState!.Energy == 0
                && spare.Pile?.Type == PileType.Hand, "Initiative freely places exactly the selected hand card");
            check(!AmbushZone.IsReadyForAmbush(chosen) && card.Pile?.Type == PileType.Exhaust,
                "Initiative itself exhausts through native play and preserves the placed card's waiting turn");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<LingdawnpaoInitiative>(p, s, PileType.Play); card.UpgradeInternal();
            var old = Add<Ingenuity>(p, s); Place(p, old); AmbushZone.For(p).BuriedTurn[old] = -1;
            var first = Add<WhetClaw>(p, s); var second = Add<Ingenuity>(p, s);
            var spare = Add<Ingenuity>(p, s); var stance = Add<AmbushStance>(p, s);
            sel.Steps.Enqueue((options, min, max) => {
                check(min == 2 && max == 2 && !options.Contains(card) && !options.Contains(stance),
                    "upgraded Initiative selects two cards and excludes the source and stance token");
                return [first, second];
            });
            Play(card);
            check(AmbushZone.For(p).Cards.SequenceEqual(new CardModel[] { old, first, second })
                && AmbushZone.For(p).BuriedTurn[old] == -1, "Initiative preserves old regional cards, FIFO order and their burial age");
            check(spare.Pile?.Type == PileType.Hand && stance.Pile?.Type == PileType.Hand
                && !AmbushZone.IsReadyForAmbush(first) && !AmbushZone.IsReadyForAmbush(second),
                "upgraded Initiative places only the chosen cards without granting immediate Ambush");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<LingdawnpaoInitiative>(p, s, PileType.Play); card.UpgradeInternal();
            while (AmbushZone.For(p).Count < AmbushZone.For(p).Capacity - 1) Place(p, Add<Ingenuity>(p, s));
            var chosen = Add<WhetClaw>(p, s); var spare = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => {
                check(min == 1 && max == 1, "Initiative's selection is capped by the one remaining regional slot");
                return [chosen];
            });
            Play(card);
            check(AmbushZone.For(p).IsFull && AmbushZone.For(p).Cards.Contains(chosen) && spare.Pile?.Type == PileType.Hand,
                "Initiative leaves overflow candidates in hand");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<LingdawnpaoInitiative>(p, s, PileType.Play); card.UpgradeInternal();
            var only = Add<WhetClaw>(p, s); Play(card);
            check(sel.Calls == 0 && AmbushZone.For(p).Cards.Contains(only),
                "upgraded Initiative automatically places a lone eligible card without an impossible two-card choice");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<LingdawnpaoInitiative>(p, s, PileType.Play); var stance = Add<AmbushStance>(p, s); Play(card);
            check(sel.Calls == 0 && AmbushZone.For(p).Count == 0 && stance.Pile?.Type == PileType.Hand,
                "Initiative completes with a stance-only hand without opening an empty selector");
        });
        foreach (bool disabled in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<LingdawnpaoInitiative>(p, s, PileType.Play);
            if (!disabled) while (!AmbushZone.For(p).IsFull) Place(p, Add<Ingenuity>(p, s));
            AmbushZone.For(p).IsCoolingDown = disabled;
            var keep = Add<WhetClaw>(p, s); int before = AmbushZone.For(p).Count; Play(card);
            check(sel.Calls == 0 && AmbushZone.For(p).Count == before && keep.Pile?.Type == PileType.Hand,
                "full or disabled region stops Initiative without a dead selection or moving cards");
        });

        // One up-front block event uses the original count and Dexterity only once.
        foreach (int count in new[] { 1, 2, 3, 5 })
        foreach (int permanentDexterity in new[] { 0, 2 }) Scenario((p, s, sel) =>
        {
            var card = Add<HuntOperation>(p, s, PileType.Play);
            if (count > 3) PowerCmd.Apply<AmbushCapacityPower>(context, p.Creature, 5m, p.Creature, null).GetAwaiter().GetResult();
            if (permanentDexterity > 0)
                PowerCmd.Apply<DexterityPower>(context, p.Creature, permanentDexterity, p.Creature, null).GetAwaiter().GetResult();
            var stored = Enumerable.Range(0, count).Select(_ => Add<DeepWinter>(p, s)).ToArray();
            foreach (var skill in stored) Place(p, skill);
            Play(card);
            int expected = 2 * count + count + permanentDexterity;
            var gains = CombatManager.Instance.History.Entries.OfType<BlockGainedEntry>()
                .Where(e => e.CardPlay?.Card == card).ToArray();
            check(p.Creature.Block == expected && gains.Length == 1 && gains[0].Amount == expected,
                $"{card.Id.Entry} grants one up-front block event with original regional and permanent Dexterity (expected {expected}, got {p.Creature.Block})");
            check(AmbushZone.For(p).Count == 0 && stored.All(c => c.Pile?.Type == PileType.Discard),
                $"{card.Id.Entry} resolves every brought-out skill before finishing the sweep");
        });
        foreach (bool disabled in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<HuntOperation>(p, s, PileType.Play);
            PowerCmd.Apply<DexterityPower>(context, p.Creature, 3m, p.Creature, null).GetAwaiter().GetResult();
            if (disabled) Place(p, Add<DeepWinter>(p, s));
            AmbushZone.For(p).IsCoolingDown = disabled;
            Play(card);
            check(p.Creature.Block == 0 && AmbushZone.For(p).Count == (disabled ? 1 : 0),
                "empty or disabled sweeps grant no block, including from Dexterity, and finish without a loop");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<HuntOperation>(p, s, PileType.Play); card.UpgradeInternal();
            PowerCmd.Apply<DexterityPower>(context, p.Creature, 2m, p.Creature, null).GetAwaiter().GetResult();
            for (int i = 0; i < 3; i++) Place(p, Add<DeepWinter>(p, s));
            Play(card);
            var gains = CombatManager.Instance.History.Entries.OfType<BlockGainedEntry>()
                .Where(e => e.CardPlay?.Card == card).ToArray();
            check(card.Keywords.Contains(CardKeyword.Retain) && card.DynamicVars.CalculationExtra.BaseValue == 3
                && p.Creature.Block == 14 && gains.Length == 1 && AmbushZone.For(p).Count == 0,
                "Unleash upgrades to three Block per stored card plus Retain, with Dexterity counted once");
        });

        foreach (bool upgraded in new[] { false, true })
        foreach (bool skills in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<SnowSift>(p, s, PileType.Play); if (upgraded) card.UpgradeInternal();
            var old = skills ? Add<DeepWinter>(p, s) : (CardModel)Add<ClawSwipe>(p, s); Place(p, old);
            var recovered = Add<FurGuard>(p, s, PileType.Discard);
            sel.Steps.Enqueue((options, min, max) => {
                check(p.Creature.Block == (upgraded ? 5 : 3) + (skills ? 1 : 0)
                    && old.Pile?.Type == PileType.Discard && options.Contains(recovered),
                    "Snow Sift gains Block before losing regional Dexterity, then brings the old card out before selection");
                return [recovered];
            });
            CardCmd.AutoPlay(context, card, null, skipCardPileVisuals: true).GetAwaiter().GetResult();
            check(AmbushZone.For(p).Cards.SequenceEqual(new[] { recovered }) && !AmbushZone.IsReadyForAmbush(recovered)
                && card.Pile?.Type == (upgraded ? PileType.Discard : PileType.Exhaust),
                "Snow Sift preserves Exhaust/upgrade, places the selected discard freshly and never immediately brings it back out");
        });

        foreach (bool upgraded in new[] { false, true })
        foreach (bool mature in new[] { false, true })
        foreach (int echoes in new[] { 0, 1 }) Scenario((p, s, sel) =>
        {
            var card = Add<Ingenuity>(p, s); if (upgraded) card.UpgradeInternal(); Place(p, card);
            if (mature) AmbushZone.For(p).BuriedTurn[card] = -1;
            if (echoes > 0) PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, echoes, p.Creature, null).GetAwaiter().GetResult();
            AmbushZone.BringOutFirstAsync(context, null, p).GetAwaiter().GetResult();
            int expected = (upgraded ? 7 : 5) + (mature ? 5 * (1 + echoes) : 0);
            check(p.Creature.Block == expected,
                "Ingenuity shifts one Block to its base, keeps its ordinary mature total, and repeats only the four-point bonus");
            check(AmbushZone.For(p).Effects.Count == (mature ? 1 : 0)
                && (!mature || AmbushZone.For(p).Effects.Snapshot()[0].Amount == 4),
                "only a mature Ingenuity records its four-point bonus for Barrage");
        });

        foreach (bool throat in new[] { false, true })
        foreach (bool upgraded in new[] { false, true })
        foreach (bool mature in new[] { false, true })
        foreach (int echoes in new[] { 0, 1 }) Scenario((p, s, sel) =>
        {
            CardModel card = throat ? Add<ThroatGrip>(p, s) : Add<SneakAttack>(p, s);
            if (upgraded) card.UpgradeInternal(); Place(p, card);
            if (mature) AmbushZone.For(p).BuriedTurn[card] = -1;
            if (echoes > 0) PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, echoes, p.Creature, null).GetAwaiter().GetResult();
            var enemy = s.HittableEnemies[0];
            AmbushZone.BringOutFirstAsync(context, enemy, p).GetAwaiter().GetResult();
            int baseDamage = (throat ? upgraded ? 16 : 12 : upgraded ? 8 : 6) + 1;
            int bonus = throat ? upgraded ? 18 : 13 : upgraded ? 7 : 5;
            int repeats = mature ? 1 + echoes : 0;
            decimal debuff = throat ? enemy.GetPower<VulnerablePower>()?.Amount ?? 0 : enemy.GetPower<WeakPower>()?.Amount ?? 0;
            int expectedDebuff = (throat ? 2 : upgraded ? 2 : 1) + repeats;
            check(enemy.CurrentHp == 200 - baseDamage - bonus * repeats && debuff == expectedDebuff,
                "source Ambush damage and one extra Weak/Vulnerable resolve only when mature and repeat normally with Echo");
            var recorded = AmbushZone.For(p).Effects.Snapshot();
            check(recorded.Length == (mature ? 1 : 0) && (!mature || recorded[0].Secondary == 1),
                "a source card records only its additional debuff, once per card name, rather than its base debuff");
        });

        foreach (bool throat in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var source = Add<AmbushBarrage>(p, s);
            AmbushZone.For(p).Effects.Record(new(throat ? AmbushEffectKind.ThroatGrip : AmbushEffectKind.SneakAttack,
                throat ? 8 : 4, 1));
            Place(p, source); AmbushZone.For(p).BuriedTurn[source] = -1;
            var enemy = s.HittableEnemies[0];
            AmbushZone.BringOutFirstAsync(context, enemy, p).GetAwaiter().GetResult();
            check(enemy.CurrentHp == (throat ? 181 : 189)
                && (throat ? enemy.GetPower<VulnerablePower>()?.Amount : enemy.GetPower<WeakPower>()?.Amount) == 1,
                "Barrage inherits one additional debuff, applies it before its bonus attack and does not copy the source's base debuff");
            check(AmbushZone.For(p).Effects.Count == 1, "a mixed damage/debuff Ambush remains one recorded card name");
        });
        foreach (bool upgraded in new[] { false, true })
        foreach (bool mature in new[] { false, true })
        foreach (int echoes in new[] { 0, 1 }) Scenario((p, s, sel) =>
        {
            var card = Add<DamBreak>(p, s); if (upgraded) card.UpgradeInternal(); Place(p, card);
            if (mature) AmbushZone.For(p).BuriedTurn[card] = -1;
            if (echoes > 0) PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, echoes, p.Creature, null).GetAwaiter().GetResult();
            var enemy = s.HittableEnemies[0];
            AmbushZone.BringOutFirstAsync(context, enemy, p).GetAwaiter().GetResult();
            int hit = upgraded ? 13 : 9;
            int weak = upgraded ? 2 : 1;
            int repeats = mature ? 1 + echoes : 0;
            var effects = AmbushZone.For(p).Effects.Snapshot();
            check(card.EnergyCost.Canonical == 1 && card.Rarity == CardRarity.Ancient && card.Type == CardType.Attack
                && enemy.CurrentHp == 200 - hit * (1 + repeats)
                && enemy.GetPower<WeakPower>()?.Amount == weak * (1 + repeats),
                "Surprise Attack stays at one cost and natively resolves 8/12 damage plus 1/2 Weak in both base and mature bonus");
            check(effects.Length == (mature ? 1 : 0) && (!mature || effects[0].Kind == AmbushEffectKind.DamBreak
                && effects[0].Amount == (upgraded ? 12 : 8) && effects[0].Secondary == weak),
                "the ancient records its own name and exact upgraded bonus rather than being mistaken for ordinary Sneak Attack");
        });
        foreach (bool upgraded in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var source = Add<AmbushBarrage>(p, s); var zone = AmbushZone.For(p);
            zone.Effects.Record(new(AmbushEffectKind.DamBreak, upgraded ? 12 : 8, upgraded ? 2 : 1, upgraded));
            Place(p, source); zone.BuriedTurn[source] = -1; var enemy = s.HittableEnemies[0];
            AmbushZone.BringOutFirstAsync(context, enemy, p).GetAwaiter().GetResult();
            check(enemy.CurrentHp == (upgraded ? 181 : 185) && enemy.GetPower<WeakPower>()?.Amount == (upgraded ? 2 : 1)
                && zone.Effects.Count == 1,
                "Barrage inherits the ancient's 8/12 damage and 1/2 Weak exactly once without copying base Weak");
        });
        {
            var map = new Dictionary<ModelId, CardModel> {
                [ModelDb.Card<HuntOperation>().Id] = ModelDb.Card<DamBreak>(),
                [ModelDb.Card<Ingenuity>().Id] = ModelDb.Card<Ingenuity>() };
            var patch = typeof(FrostFang).Assembly.GetTypes().Single(t => t.Name == "ArchaicTooth_TranscendenceUpgrades_SneakAttackPatch");
            patch.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { map });
            check(!map.ContainsKey(ModelDb.Card<HuntOperation>().Id)
                && map[ModelDb.Card<SneakAttack>().Id].Id == ModelDb.Card<DamBreak>().Id
                && map.ContainsKey(ModelDb.Card<Ingenuity>().Id),
                "Archaic Tooth maps Sneak Attack to Surprise Attack, removes the old Unleash route and preserves unrelated mappings");
        }
        Scenario((p, s, sel) =>
        {
            var source = Add<AmbushBarrage>(p, s); var zone = AmbushZone.For(p);
            zone.Effects.Record(new(AmbushEffectKind.ThroatGrip, 8, 1));
            zone.Effects.Record(new(AmbushEffectKind.SneakAttack, 4, 1));
            zone.Effects.Record(new(AmbushEffectKind.WhetClaw));
            PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, 1m, p.Creature, null).GetAwaiter().GetResult();
            Place(p, source); zone.BuriedTurn[source] = -1; var enemy = s.HittableEnemies[0];
            AmbushZone.BringOutFirstAsync(context, enemy, p).GetAwaiter().GetResult();
            check(enemy.CurrentHp == 95 && enemy.GetPower<WeakPower>()?.Amount == 4
                && enemy.GetPower<VulnerablePower>()?.Amount == 4 && zone.Effects.Count == 3,
                "Echo and Whet repeat the new debuffs exactly four times without recursive collection or extra effect names");
        });
        Scenario((p, s, sel) =>
        {
            var second = new Creature(ModelDb.Monster<Byrdpip>().ToMutable(), CombatSide.Enemy, null);
            second.SetMaxHpInternal(200); second.SetCurrentHpInternal(200);
            typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(second, s); s.AddCreature(second);
            var source = Add<AmbushBarrage>(p, s); var zone = AmbushZone.For(p);
            zone.Effects.Record(new(AmbushEffectKind.AmbushPounce, 2));
            zone.Effects.Record(new(AmbushEffectKind.ThroatGrip, 8, 1));
            zone.Effects.Record(new(AmbushEffectKind.SneakAttack, 4, 1));
            Place(p, source); zone.BuriedTurn[source] = -1; var first = s.HittableEnemies[0];
            AmbushZone.BringOutFirstAsync(context, first, p).GetAwaiter().GetResult();
            check(first.CurrentHp == 162 && second.CurrentHp == 162
                && first.GetPower<WeakPower>()?.Amount == 1 && second.GetPower<WeakPower>()?.Amount == 1
                && first.GetPower<VulnerablePower>()?.Amount == 1 && second.GetPower<VulnerablePower>()?.Amount == 1,
                "Pounce applies both inherited debuffs to all enemies while retaining ordinary grouped attacks");
        });
        Scenario((p, s, sel) =>
        {
            var source = Add<AmbushBarrage>(p, s); var zone = AmbushZone.For(p);
            zone.Effects.Record(new(AmbushEffectKind.ThroatGrip, 8, 1));
            var enemy = s.HittableEnemies[0];
            PowerCmd.Apply<ArtifactPower>(context, enemy, 1m, enemy, null).GetAwaiter().GetResult();
            Place(p, source); zone.BuriedTurn[source] = -1;
            AmbushZone.BringOutFirstAsync(context, enemy, p).GetAwaiter().GetResult();
            check(enemy.CurrentHp == 185 && !enemy.HasPower<VulnerablePower>() && !enemy.HasPower<ArtifactPower>(),
                "inherited Vulnerable respects Artifact and cannot amplify its attack when blocked");
        });

        // The number to exhaust is independent of remaining hand and zone slots.
        Scenario((p, s, sel) =>
        {
            var card = Add<Concentration>(p, s, PileType.Play);
            var pay = Enumerable.Range(0, 3).Select(_ => Add<Ingenuity>(p, s)).ToArray();
            var stored = Add<WhetClaw>(p, s); var stance = Add<AmbushStance>(p, s);
            sel.Steps.Enqueue((options, min, max) => {
                check(min == 0 && max == 4 && !options.Contains(stance), "Concentration can exhaust more than half the hand and excludes the stance");
                return pay;
            });
            Play(card);
            check(pay.All(c => c.Pile?.Type == PileType.Exhaust), "Concentration uses native hand exhaustion for every chosen payment");
            check(AmbushZone.For(p).Cards.Contains(stored) && sel.Calls == 1,
                $"fewer remaining cards are all placed without a second selector (zone={AmbushZone.For(p).Count}, selectors={sel.Calls})");
            check(stance.Pile?.Type == PileType.Hand && p.PlayerCombatState!.Energy == 0, "stance stays visible and a two-cost card enters without spending energy");
            check(!AmbushZone.IsReadyForAmbush(stored), "Concentration placement does not bypass the Ambush waiting turn");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Concentration>(p, s, PileType.Play);
            for (int i = 0; i < AmbushZone.For(p).Capacity - 1; i++) Place(p, Add<Ingenuity>(p, s));
            var pay = new[] { Add<Ingenuity>(p, s), Add<Ingenuity>(p, s) };
            var keep = Add<Ingenuity>(p, s); var stored = Add<WhetClaw>(p, s);
            sel.Steps.Enqueue((options, min, max) => pay);
            sel.Steps.Enqueue((options, min, max) => { check(min == 1 && max == 1, "second selection is capped to the single empty zone slot"); return [stored]; });
            Play(card);
            check(pay.All(c => c.Pile?.Type == PileType.Exhaust) && AmbushZone.For(p).IsFull,
                "a small zone does not reduce the chosen exhaustion payment");
            check(AmbushZone.For(p).Cards.Contains(stored) && keep.Pile?.Type == PileType.Hand,
                "overflow candidates stay in hand rather than being lost or placed over capacity");
        });
        foreach (bool disabled in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<Concentration>(p, s, PileType.Play);
            if (!disabled) while (!AmbushZone.For(p).IsFull) Place(p, Add<Ingenuity>(p, s));
            AmbushZone.For(p).IsCoolingDown = disabled;
            var pay = Add<Ingenuity>(p, s); var keep = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [pay]);
            Play(card);
            check(pay.Pile?.Type == PileType.Exhaust && keep.Pile?.Type == PileType.Hand && sel.Calls == 1,
                "full or disabled zone still permits exhausting, then stops placement without a dead selector");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Concentration>(p, s, PileType.Play); var pay = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [pay]); Play(card);
            check(pay.Pile?.Type == PileType.Exhaust && AmbushZone.For(p).Count == 0 && sel.Calls == 1,
                "exhausting the entire selectable hand completes without waiting for a second choice");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Concentration>(p, s, PileType.Play); var keep = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => []); Play(card);
            check(keep.Pile?.Type == PileType.Hand && AmbushZone.For(p).Count == 0, "choosing zero cards preserves the hand and zone");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Concentration>(p, s, PileType.Play); Add<AmbushStance>(p, s); Play(card);
            check(sel.Calls == 0, "a stance-only hand never opens an empty selector");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Regroup>(p, s, PileType.Play); card.UpgradeInternal();
            var first = Add<Ingenuity>(p, s); var chosen = Add<Ingenuity>(p, s);
            Place(p, first); Place(p, chosen); AmbushZone.For(p).BuriedTurn[first] = -1;
            var stored = Add<WhetClaw>(p, s); var spare = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [chosen]);
            sel.Steps.Enqueue((options, min, max) => [stored]);
            Play(card);
            check(chosen.Pile?.Type == PileType.Discard && p.Creature.Block == 12,
                "Regroup auto-plays a fresh chosen card with its Ambush bonus exactly once and does not exhaust it");
            check(AmbushZone.For(p).Cards.SequenceEqual(new CardModel[] { first, stored })
                && AmbushZone.For(p).BuriedTurn[first] == -1, "Regroup preserves the unchosen FIFO card and its burial age");
            check(spare.Pile?.Type == PileType.Hand && !AmbushZone.IsReadyForAmbush(stored),
                "Regroup adds exactly one fresh hand card and does not accelerate its Ambush");
            sel.Steps.Enqueue((options, min, max) => [first]); Play(card);
            check(first.Pile?.Type == PileType.Discard && p.Creature.Block == 22 && AmbushZone.For(p).Cards.Contains(spare)
                && p.PlayerCombatState!.ExhaustPile.Cards.Count == 0,
                "reusing Regroup performs another exchange without burning either brought-out card");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Regroup>(p, s, PileType.Play); var stored = Add<Ingenuity>(p, s); Play(card);
            check(AmbushZone.For(p).Cards.Contains(stored) && p.Creature.Block == 0,
                "empty-zone Regroup can place a card without bringing that same card straight back out");
        });
        foreach (bool mature in new[] { false, true })
        foreach (int echoes in new[] { 0, 1 }) Scenario((p, s, sel) =>
        {
            var card = Add<Regroup>(p, s, PileType.Play);
            var chosen = Add<Ingenuity>(p, s); Place(p, chosen);
            if (mature) AmbushZone.For(p).BuriedTurn[chosen] = -1;
            if (echoes > 0) PowerCmd.Apply<AmbushEchoPower>(context, p.Creature, echoes, p.Creature, null).GetAwaiter().GetResult();
            var fresh = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [chosen]);
            Play(card);
            check(p.Creature.Block == 5 + 5 * (1 + echoes)
                && AmbushZone.For(p).Effects.Count == 1,
                "Regroup uses the normal Ambush trigger count for fresh or mature cards, including Echo, and records the effect");
            check(!AmbushZone.IsReadyForAmbush(fresh) && !AmbushZone.IsVeteranAmbush(chosen),
                "Regroup's bypass does not leak into newly placed cards or persist after the brought-out play");
        });
        foreach (bool mature in new[] { false, true }) Scenario((p, s, sel) =>
        {
            var card = Add<Regroup>(p, s, PileType.Play);
            var chosen = Add<WhetClaw>(p, s); Place(p, chosen);
            if (mature) AmbushZone.For(p).BuriedTurn[chosen] = -1;
            Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [chosen]);
            Play(card);
            check(s.HittableEnemies[0].CurrentHp == 174 && p.Creature.GetPower<WhetClawPower>()?.Amount == 8,
                "fresh and mature Whet Claw each resolve exactly twice through Regroup, with ordinary growth and regional Strength");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Regroup>(p, s, PileType.Play);
            AmbushZone.For(p).Effects.Record(new(AmbushEffectKind.HoldBreath, Secondary: 1m));
            var chosen = Add<AmbushBarrage>(p, s); Place(p, chosen); Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [chosen]);
            Play(card);
            check(p.PlayerCombatState!.Energy == 1 && AmbushZone.For(p).Effects.Count == 1,
                "Regroup lets a fresh Barrage resolve collected Ambush effects without collecting itself");
        });
        Scenario((p, s, sel) =>
        {
            var card = Add<Regroup>(p, s, PileType.Play);
            var chosen = Add<Ingenuity>(p, s); Place(p, chosen); var fresh = Add<Ingenuity>(p, s);
            sel.Steps.Enqueue((options, min, max) => [chosen]); Play(card);
            decimal before = p.Creature.Block;
            AmbushZone.BringOutFirstAsync(context, s.HittableEnemies[0], p).GetAwaiter().GetResult();
            check(p.Creature.Block - before == 5 && fresh.Pile?.Type == PileType.Discard,
                "an ordinary bring-out of Regroup's newly placed card still grants only its base effect that turn");
        });
    }
}
