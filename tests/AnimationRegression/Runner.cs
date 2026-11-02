using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

public partial class Runner : Node2D
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private int checks;
    private void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
    private void Set(object target, string name, object value) => target.GetType().GetField(name, All)!.SetValue(target, value);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private Player PlayerFixture(ulong id)
    {
        var p=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        Set(p, "<NetId>k__BackingField", id);
        Set(p, "<Character>k__BackingField", ModelDb.Character<Lingdawnpao>());
        Set(p, "<Creature>k__BackingField", new Creature(p,72,72));
        return p;
    }

    public override async void _Ready()
    {
        try
        {
            TestMode.IsOn=true;
            Check(ClassDB.ClassExists("SpineSprite"), "real Spine GDExtension is loaded");
            Check(ProjectSettings.LoadResourcePack("D:/sts2mod/lingdawnpao/build/lingdawnpao.pck"), "new release pack loads");
            var data=ResourceLoader.Load<Resource>("res://animations/characters/lingdawnpao/v6_motion/snow_leopard_v6_skel_data.tres");
            Check(data!=null, "new skeleton, atlas and imported pages load from the release pack");
            ModelDb.Inject(typeof(Lingdawnpao));
            var player=PlayerFixture(14701);
            AmbushZone.For(player).IsAmbushMode=false;
            var actor=new TestCreature { Position=new Vector2(470,500) };
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature).GetProperty("Entity",All)!.SetValue(actor,player.Creature);
            var sprite=ClassDB.Instantiate("SpineSprite").As<Node2D>();
            sprite.Set("skeleton_data_res",data!);
            sprite.Scale=Vector2.One*.7f;
            actor.AddChild(sprite);
            AddChild(actor);
            await Wait(.05);
            var controller=new MegaSprite(Variant.From(sprite));
            foreach(string name in new[]{"idle_loop","low_health_loop","attack","cast","hurt","die","ground_palm","ambush_enter","ambush_idle","ambush_low_health_loop","ambush_exit","ambush_hurt","ambush_die"})
                Check(controller.HasAnimation(name),"packed skeleton has "+name);
            var animator=(CreatureAnimator)typeof(Lingdawnpao).GetMethod("CreateIdleAnimator",All)!.Invoke(null,[controller])!;
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature).GetField("_spineAnimator",All)!.SetValue(actor,animator);
            var patch=typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.LingdawnpaoAmbushAnimationPatch")!;
            Check(new Harmony("lingdawnpao.animation-regression").CreateClassProcessor(patch).Patch().Count>0,
                "ambush animation guard binds to the real trigger method");
            Check(new Harmony("lingdawnpao.low-health-regression").CreateClassProcessor(
                typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.LingdawnpaoLowHealthAnimationPatch")!).Patch().Count>0,
                "health return correction binds to native animation callback");
            string? Current()=>controller.GetAnimationState().GetCurrentAnimationName();
            Check(Current()=="idle_loop","normal mode starts in standing idle");
            actor.SetAnimationTrigger("Attack");
            Check(Current()=="attack","normal attack plays the dedicated animation");
            await Wait(1.3);
            Check(Current()=="idle_loop","normal attack returns to standing idle");
            actor.SetAnimationTrigger("Hit");
            Check(Current()=="hurt","standing hit plays recoil");
            await Wait(.08);
            actor.SetAnimationTrigger("Hit");
            await Wait(.65);
            Check(Current()=="idle_loop","consecutive standing hits recover to idle");
            AmbushZone.For(player).IsAmbushMode=true;
            var modeType=typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.LingdawnpaoAnimationController")!;
            Check(ReferenceEquals(modeType.GetMethod("FindCreature",All)!.Invoke(null,[controller]),player.Creature),
                "animator resolves the correct owning creature from the native sprite");
            Check((bool)modeType.GetMethod("ShouldSuppress",All,null,[typeof(Creature),typeof(string)],null)!.Invoke(null,[player.Creature,"Attack"])!,
                "ambush guard recognizes the live owner and mode");
            Check((bool)modeType.GetMethod("ShouldSuppress",All,null,[typeof(CreatureAnimator),typeof(string)],null)!.Invoke(null,[animator,"Attack"])!,
                "animator ownership retains the correct creature");
            actor.SetAnimationTrigger("AmbushEnter");
            Check(Current()=="ambush_enter","enter transition plays");
            await Wait(.8);
            Check(Current()=="ambush_idle","enter transition resolves to crouching loop");
            float before=controller.GetAnimationState().GetCurrent(0)!.GetTrackTime();
            foreach(string trigger in new[]{"Attack","Cast","PowerUp","GroundPalm"}) actor.SetAnimationTrigger(trigger);
            Check(Current()=="ambush_idle" && Mathf.IsEqualApprox(controller.GetAnimationState().GetCurrent(0)!.GetTrackTime(),before),
                "burial/auto-play triggers preserve the existing ambush loop without restarting it");
            await Wait(.2);
            Check(controller.GetAnimationState().GetCurrent(0)!.GetTrackTime()>0,"ambush breathing keeps advancing");
            actor.SetAnimationTrigger("Hit");
            Check(Current()=="ambush_hurt","taking a hit plays recoil while crouching");
            await Wait(.65);
            Check(Current()=="ambush_idle","ambush recoil returns to the crouching loop");
            actor.SetAnimationTrigger("Hit");
            await Wait(.06);
            actor.SetAnimationTrigger("Hit");
            await Wait(.65);
            Check(Current()=="ambush_idle","consecutive ambush hits leave no stale reaction queued");
            actor.SetAnimationTrigger("Hit");
            AmbushZone.For(player).IsAmbushMode=false;
            actor.SetAnimationTrigger("AmbushExit");
            await Wait(.8);
            Check(Current()=="idle_loop","leaving ambush during recoil cancels the old crouching return");
            for(int i=0;i<20;i++)
            {
                AmbushZone.For(player).IsAmbushMode=false;
                actor.SetAnimationTrigger("AmbushExit");
                AmbushZone.For(player).IsAmbushMode=true;
                actor.SetAnimationTrigger("AmbushEnter");
            }
            await Wait(.8);
            Check(Current()=="ambush_idle","rapid toggles leave no stale standing animation queued");
            using(var capture=GetViewport().GetTexture().GetImage())
                capture.SavePng("D:/sts2mod/lingdawnpao/build/ambush-native-capture.png");
            AmbushZone.For(player).IsAmbushMode=false;
            actor.SetAnimationTrigger("AmbushExit");
            await Wait(.8);
            Check(Current()=="idle_loop","exit transition returns to standing idle");
            void Hp(int value) => typeof(Creature).GetProperty("CurrentHp",All)!.SetValue(player.Creature,value);
            Hp(19);
            Check(Current()=="idle_loop","above quarter health keeps normal breathing");
            Hp(18);
            Check(Current()=="low_health_loop","quarter health immediately starts fatigue breathing");
            actor.SetAnimationTrigger("Hit");
            await Wait(.1);
            Hp(30);
            Check(Current()=="hurt","healing does not cut a reaction short");
            await Wait(.65);
            Check(Current()=="idle_loop","healing during recoil corrects the queued idle");
            actor.SetAnimationTrigger("Attack");
            Hp(17);
            Check(Current()=="attack","low health does not interrupt an attack");
            await Wait(1.3);
            Check(Current()=="low_health_loop","attack returns to the current health posture");
            AmbushZone.For(player).IsAmbushMode=true;
            actor.SetAnimationTrigger("AmbushEnter");
            await Wait(.8);
            Check(Current()=="ambush_low_health_loop","low health keeps the leopard hidden while ambushing");
            Hp(72);
            Check(Current()=="ambush_idle","healing in ambush restores crouching breathing");
            AmbushZone.For(player).IsAmbushMode=false;
            actor.SetAnimationTrigger("AmbushExit");
            await Wait(.8);
            actor.SetAnimationTrigger("GroundPalm");
            Check(Current()=="ground_palm","dedicated palm trigger is available");
            actor.SetAnimationTrigger("Hit");
            await Wait(.05);
            Set(player,"<Creature>k__BackingField",new Creature(player,0,72));
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature).GetProperty("Entity",All)!.SetValue(actor,player.Creature);
            actor.SetAnimationTrigger("Dead");
            Check(Current()=="die","lethal standing hit interrupts recoil with death");
            actor.SetAnimationTrigger("Hit");actor.SetAnimationTrigger("Idle");actor.SetAnimationTrigger("Cast");
            await Wait(2.1);
            var standingDeath=(AnimState)typeof(CreatureAnimator).GetField("_currentState",All)!.GetValue(animator)!;
            Check(Current()=="die" && !standingDeath.IsLooping && standingDeath.NextState==null,
                "standing death finishes without looping or returning to idle");
            using(var capture=GetViewport().GetTexture().GetImage())
            {
                capture.SavePng("D:/sts2mod/lingdawnpao/build/standing-death-native-capture.png");
                var pixels=capture.GetData();
                await Wait(.2);
                using var held=GetViewport().GetTexture().GetImage();
                Check(pixels.SequenceEqual(held.GetData()),"standing corpse remains motionless after completion");
            }
            Set(player,"<Creature>k__BackingField",new Creature(player,72,72));
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature).GetProperty("Entity",All)!.SetValue(actor,player.Creature);
            actor.SetAnimationTrigger("Revive");
            Check(Current()=="idle_loop","standing revive restores standing idle");
            AmbushZone.For(player).IsAmbushMode=true;
            actor.SetAnimationTrigger("AmbushEnter");
            await Wait(.7);
            actor.SetAnimationTrigger("Hit");
            await Wait(.05);
            var dead=new Creature(player,0,72);
            Set(player,"<Creature>k__BackingField",dead);
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature).GetProperty("Entity",All)!.SetValue(actor,dead);
            actor.SetAnimationTrigger("Dead");
            Check(Current()=="ambush_die","death is allowed while ambushing");
            actor.SetAnimationTrigger("Idle"); actor.SetAnimationTrigger("Attack"); actor.SetAnimationTrigger("Hit");
            await Wait(2.1);
            var state=(AnimState)typeof(CreatureAnimator).GetField("_currentState",All)!.GetValue(animator)!;
            Check(Current()=="ambush_die" && !state.IsLooping && state.NextState==null,
                "death is terminal and late callbacks cannot restart idle");
            using(var capture=GetViewport().GetTexture().GetImage())
            {
                capture.SavePng("D:/sts2mod/lingdawnpao/build/ambush-death-native-capture.png");
                var pixels=capture.GetData();
                await Wait(.2);
                using var held=GetViewport().GetTexture().GetImage();
                Check(pixels.SequenceEqual(held.GetData()),"ambush corpse and released shrub remain motionless after completion");
            }
            Set(player,"<Creature>k__BackingField",new Creature(player,72,72));
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NCreature).GetProperty("Entity",All)!.SetValue(actor,player.Creature);
            actor.SetAnimationTrigger("Revive");
            Check(Current()=="ambush_idle","revive respects the selected ambush mode");
            actor.QueueFree();
            await Wait(.1);
            data!.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Wait(.1);
            GD.Print($"PASS: {checks} native Spine animation regression checks.");
            GetTree().Quit(0);
        }
        catch(Exception exception) { GD.PrintErr(exception);GetTree().Quit(1); }
    }
}
