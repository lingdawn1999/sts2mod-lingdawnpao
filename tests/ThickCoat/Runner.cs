using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

public partial class Runner : Node
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private int checks;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, All)!.SetValue(o,value);
    private static readonly ThrowingPlayerChoiceContext Context = new();
    private static readonly MethodInfo Place = typeof(ThickCoat).GetMethod("ResolvePlacementAsync",All)!;
    private static Task Placement(ThickCoat card, CardPlay play) => (Task)Place.Invoke(card,[Context,play])!;
    private static CardPlay Play(ThickCoat card, bool auto=false, int index=0, int count=1, PileType result=PileType.Discard)
    {
        var play=(CardPlay)RuntimeHelpers.GetUninitializedObject(typeof(CardPlay));
        foreach(var pair in new (string name, object value)[] {
            (nameof(CardPlay.Card),card),(nameof(CardPlay.IsAutoPlay),auto),(nameof(CardPlay.PlayIndex),index),
            (nameof(CardPlay.PlayCount),count),(nameof(CardPlay.ResultPile),result) })
            typeof(CardPlay).GetProperty(pair.name)!.SetValue(play,pair.value);
        return play;
    }
    private static (Player owner, ThickCoat card, CombatState combat) Fixture(ulong id)
    {
        var owner=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        Set(owner,"<NetId>k__BackingField",id);
        Set(owner,"<Creature>k__BackingField",new Creature(owner,72,72));
        Set(owner,"<Character>k__BackingField",ModelDb.Character<Lingdawnpao>());
        Set(owner,"_runState",NullRunState.Instance);
        Set(owner,"_relics",new List<RelicModel>());
        Set(owner,"_potionSlots",new List<PotionModel?>());
        Set(owner,"_runPiles",Array.Empty<CardPile>());
        var state=(PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        Set(state,"_pets",new List<Creature>());
        var piles=new[]{new CardPile(PileType.Hand),new CardPile(PileType.Draw),new CardPile(PileType.Discard),
            new CardPile(PileType.Exhaust),new CardPile(PileType.Play)};
        Set(state,"_piles",piles);
        string[] names=["Hand","DrawPile","DiscardPile","ExhaustPile","PlayPile"];
        for(int i=0;i<piles.Length;i++)Set(state,$"<{names[i]}>k__BackingField",piles[i]);
        Set(state,"<OrbQueue>k__BackingField",new MegaCrit.Sts2.Core.Entities.Orbs.OrbQueue(owner));
        typeof(Player).GetProperty(nameof(Player.PlayerCombatState))!.SetValue(owner,state);
        var combat=new CombatState();combat.AddPlayer(owner);
        var enemy=new Creature(ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.Byrdpip>().ToMutable(),CombatSide.Enemy,null);
        typeof(Creature).GetProperty(nameof(Creature.CombatState))!.SetValue(enemy,combat);
        combat.AddCreature(enemy);
        var card=(ThickCoat)ModelDb.Card<ThickCoat>().ToMutable();
        combat.AddCard(card,owner);state.PlayPile.AddInternal(card);
        return (owner,card,combat);
    }
    private static Action EnterCombat(CombatState state)
    {
        var manager=CombatManager.Instance;var type=manager.GetType();
        var turnField=type.GetField("_turnState",All);
        if(turnField!=null)
        {
            var previous=turnField.GetValue(manager);var turn=Activator.CreateInstance(turnField.FieldType,[state])!;
            turnField.FieldType.GetProperty("IsInProgress")!.SetValue(turn,true);
            turnField.FieldType.GetProperty("IsStarting")!.SetValue(turn,false);
            turnField.SetValue(manager,turn);return ()=>turnField.SetValue(manager,previous);
        }
        var field=type.GetField("_state",All)!;var progress=type.GetProperty("IsInProgress")!;var start=type.GetProperty("IsStarting")!;
        var oldState=field.GetValue(manager);var oldProgress=progress.GetValue(manager);var oldStart=start.GetValue(manager);
        field.SetValue(manager,state);progress.SetValue(manager,true);start.SetValue(manager,false);
        return ()=>{field.SetValue(manager,oldState);progress.SetValue(manager,oldProgress);start.SetValue(manager,oldStart);};
    }
    public override async void _Ready()
    {
        try
        {
            TestMode.IsOn=true;
            foreach(var type in new[]{typeof(Lingdawnpao),typeof(ThickCoat),typeof(FrostFang),typeof(AmbushZonePower),
                typeof(MegaCrit.Sts2.Core.Models.Powers.DexterityPower),typeof(MegaCrit.Sts2.Core.Models.Powers.StrengthPower),
                typeof(MegaCrit.Sts2.Core.Models.Monsters.Byrdpip)}) ModelDb.Inject(type);
            var (owner,card,combat)=Fixture(2201);var zone=AmbushZone.For(owner);
            await Placement(card,Play(card));
            Check(zone.Cards.Contains(card) && zone.Count==1 && owner.PlayerCombatState!.PlayPile.Cards.Count==0,
                "manual play stores the original card without immediately bringing itself out");
            Check(zone.AppliedDex==1 && zone.BuriedTurn.ContainsKey(card) && !AmbushZone.IsReadyForAmbush(card),
                "storage grants regional Dexterity and burial age without an extra Ambush effect");
            var relic=(FrostFang)ModelDb.Relic<FrostFang>().ToMutable();
            typeof(RelicModel).GetProperty(nameof(RelicModel.Owner))!.SetValue(relic,owner);
            await relic.AfterCardPlayed(Context,Play(card));
            Check(zone.Count==1 && zone.Cards.Contains(card),"the relic does not bring out the newly stored card a second time");
            zone.Pile.RemoveInternal(card);owner.PlayerCombatState!.PlayPile.AddInternal(card);
            await Placement(card,Play(card,auto:true));
            Check(card.Pile?.Type==PileType.Play && zone.Count==0,"automatic play never stores itself again");
            var (replayOwner,replayCard,replayCombat)=Fixture(2202);
            await Placement(replayCard,Play(replayCard,index:0,count:2));
            Check(replayCard.Pile?.Type==PileType.Play && AmbushZone.For(replayOwner).Count==0,"replay waits until the final effect");
            await Placement(replayCard,Play(replayCard,index:1,count:2));
            Check(AmbushZone.For(replayOwner).Cards.Count(c=>c==replayCard)==1,"replay stores exactly one copy after the last effect");
            foreach(var result in new[]{PileType.Exhaust,PileType.None})
            {
                var (specialOwner,specialCard,_)=Fixture((ulong)(2210+(int)result));
                await Placement(specialCard,Play(specialCard,result:result));
                Check(specialCard.Pile?.Type==PileType.Play && AmbushZone.For(specialOwner).Count==0,
                    "preserves engine result "+result);
            }
            var (coolingOwner,coolingCard,_)=Fixture(2220);AmbushZone.For(coolingOwner).IsCoolingDown=true;
            await Placement(coolingCard,Play(coolingCard));
            Check(coolingCard.Pile?.Type==PileType.Play && AmbushZone.For(coolingOwner).Count==0,"disabled zone preserves ordinary discard cleanup");
            var (fullOwner,fullCard,fullCombat)=Fixture(2221);var fullZone=AmbushZone.For(fullOwner);
            while(!fullZone.IsFull){var filler=ModelDb.Card<ThickCoat>().ToMutable();fullCombat.AddCard(filler,fullOwner);fullZone.Pile.AddInternal(filler);}
            var bury=typeof(AmbushZone).GetMethod("BuryPlayedCardAsync",All)!;
            Check(!await (Task<bool>)bury.Invoke(null,[Context,fullOwner,fullCard])! && fullCard.Pile?.Type==PileType.Play,
                "failed storage cannot detach the card from its normal pile");

            // Exercise actual FIFO AutoPlay, including the old Thick Coat's normal result-pile cleanup.
            var (fifoOwner,fifoCard,fifoCombat)=Fixture(2222);var fifoZone=AmbushZone.For(fifoOwner);
            var old=(ThickCoat)ModelDb.Card<ThickCoat>().ToMutable();fifoCombat.AddCard(old,fifoOwner);fifoZone.Pile.AddInternal(old);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId=fifoOwner.NetId;
            var restore=EnterCombat(fifoCombat);
            try
            {
                var onPlay=typeof(ThickCoat).GetMethod("OnPlay",All)!;
                await (Task)onPlay.Invoke(fifoCard,[Context,Play(fifoCard)])!;
                Check(fifoZone.Cards.SequenceEqual(new[]{fifoCard}) && old.Pile?.Type==PileType.Discard,
                    "playing a new coat brings out the old coat once, discards it and leaves only the new coat stored");
                Check(fifoOwner.Creature.Block==fifoCard.DynamicVars.Block.BaseValue+old.DynamicVars.Block.BaseValue,
                    "both the new play and the old automatic play resolve their block");
            }
            finally{restore();}
            GD.Print($"PASS: {checks} native Thick Coat regression checks.");GetTree().Quit(0);
        }
        catch(Exception exception){GD.PrintErr(exception);GetTree().Quit(1);}
    }
}
