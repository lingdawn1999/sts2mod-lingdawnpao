using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

public partial class Runner : Node2D
{
    private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static readonly Type Adapter=typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.LingdawnpaoRestSiteVisuals")!;
    private int checks;
    private void Check(bool condition,string text){if(!condition)throw new Exception(text);checks++;}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static bool Apply(NRestSiteCharacter avatar)=>(bool)Adapter.GetMethod("TryApply",All)!.Invoke(null,[avatar])!;
    private static Resource? Data(NRestSiteCharacter avatar)=>avatar.GetNode("SpineSprite").Get("skeleton_data_res").As<Resource>();
    private static MegaSprite Controller(NRestSiteCharacter avatar)=>new(Variant.From(avatar.GetNode("SpineSprite")));
    private static void Own(Node root,Node parent,Node child)
    {
        parent.AddChild(child);child.Owner=root;child.UniqueNameInOwner=true;
    }
    private NRestSiteCharacter Fixture(int act,Resource original,bool leopard=true,bool complete=true)
    {
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        typeof(Player).GetField("<NetId>k__BackingField",All)!.SetValue(player,(ulong)(2900+act));
        CharacterModel character=leopard?ModelDb.Character<Lingdawnpao>():ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>();
        typeof(Player).GetField("<Character>k__BackingField",All)!.SetValue(player,character);
        typeof(Player).GetField("<Creature>k__BackingField",All)!.SetValue(player,new Creature(player,72,72));
        var run=DispatchProxy.Create<IRunState,ActRunProxy>();((ActRunProxy)(object)run).ActIndex=act;
        typeof(Player).GetField("_runState",All)!.SetValue(player,run);
        var avatar=new NRestSiteCharacter{Name="Rest"+act,Position=new Vector2(440,280),Scale=Vector2.One*.760006f};
        typeof(NRestSiteCharacter).GetProperty(nameof(NRestSiteCharacter.Player))!.SetValue(avatar,player);
        typeof(NRestSiteCharacter).GetField("_characterIndex",All)!.SetValue(avatar,act);
        var sprite=ClassDB.Instantiate("SpineSprite").As<Node2D>();sprite.Name="SpineSprite";
        sprite.Set("skeleton_data_res",original);avatar.AddChild(sprite);
        if(complete)
        {
            var controls=new Control{Name="ControlRoot"};Own(avatar,avatar,controls);
            Own(avatar,controls,new TestReticle{Name="SelectionReticle"});
            Own(avatar,controls,new Control{Name="Hitbox",MouseFilter=Control.MouseFilterEnum.Ignore});
            Own(avatar,controls,new Control{Name="ThoughtBubbleLeft"});
            Own(avatar,controls,new Control{Name="ThoughtBubbleRight"});
        }
        return avatar;
    }
    public override async void _Ready()
    {
        try
        {
            TestMode.IsOn=true;
            Check(ClassDB.ClassExists("SpineSprite"),"native Spine loads");
            Check(ProjectSettings.LoadResourcePack("D:/sts2mod/lingdawnpao/build/lingdawnpao.pck"),"current mod pack loads");
            foreach(var type in new[]{typeof(Lingdawnpao),typeof(MegaCrit.Sts2.Core.Models.Characters.Ironclad)})ModelDb.Inject(type);
            string path=(string)Adapter.GetField("SkeletonPath",All)!.GetRawConstantValue()!;
            var data=ResourceLoader.Load<Resource>(path);
            var original=ResourceLoader.Load<Resource>("res://animations/merchant/lingdawnpao/shop_v1/snow_leopard_shop_skel_data.tres");
            Check(data!=null && original!=null,"rest skeleton, atlas and comparison data load");
            var patch=typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.NRestSiteCharacter_LingdawnpaoVisualsPatch")!;
            Check(new Harmony("lingdawnpao.rest-regression").CreateClassProcessor(patch).Patch().Count>0,
                "replacement binds to the actual native Ready method");
            string[] names=["overgrowth_loop","hive_loop","glory_loop"];
            var avatars=new List<NRestSiteCharacter>();
            for(int i=0;i<3;i++)
            {
                var avatar=Fixture(i,original!);avatars.Add(avatar);AddChild(avatar);
                await Wait(.15);
                Check(Data(avatar)!.GetInstanceId()==data!.GetInstanceId(),"native Ready installs the correct resting pose for act "+i);
                Check(Controller(avatar).GetAnimationState().GetCurrent(0)!.GetAnimationName()==names[i],
                    "native Ready selects the correct act animation "+i);
                Check(avatar.Scale==Vector2.One*.760006f && avatar.Position==new Vector2(440,280),
                    "native parent placement and scale remain intact for act "+i);
                Check(avatar.GetNode<Node2D>("SpineSprite").Position==new Vector2(0,280),"resting paws use the native ground baseline");
                var hitbox=avatar.Hitbox;
                Check(hitbox.OffsetLeft==-365 && hitbox.OffsetRight==330 && hitbox.OffsetTop==-80 && hitbox.OffsetBottom==335,
                    "native hitbox follows the wide reclining silhouette");
                avatar.Visible=false;
            }
            var focus=avatars[0];focus.Visible=true;var sprite=focus.GetNode<Node2D>("SpineSprite");
            var controlsRoot=focus.GetNode<Control>("ControlRoot");
            focus.FlipX();
            Check(sprite.Scale.X==-1.15f && sprite.Position.Y==280 && controlsRoot.Scale.X==-1,
                "native co-op mirror flips the model and interaction anchors together");
            var beforeMirror=sprite.Scale;
            Check(Apply(focus) && sprite.Scale==beforeMirror,"reapplying after a mirror cannot turn the player back around");
            focus.FlipX();
            Check(sprite.Scale.X==1.15f && controlsRoot.Scale.X==1,"a second mirror restores the original pose");
            foreach(var avatar in avatars)
            {
                var anchor=(Control)typeof(NRestSiteCharacter).GetMethod("GetRestSiteOptionAnchor",All)!.Invoke(avatar,null)!;
                string expected=avatar==avatars[2]?"ThoughtBubbleRight":"ThoughtBubbleLeft";
                Check(anchor.Name==expected && anchor.OffsetTop==-130,"native option bubble uses its matching adjusted anchor");
            }
            var controller=Controller(focus);await Wait(.1);
            float before=controller.GetAnimationState().GetCurrent(0)!.GetTrackTime();
            Check(Apply(focus) && Mathf.IsEqualApprox(before,controller.GetAnimationState().GetCurrent(0)!.GetTrackTime()),
                "repeat application does not restart the breathing loop");
            controller.GetAnimationState().GetCurrent(0)!.SetTimeScale(0);await Wait(.1);
            using(var capture=GetViewport().GetTexture().GetImage())
            {
                capture.SavePng("D:/sts2mod/lingdawnpao/build/rest-native-capture.png");
                var lit=capture.GetData();focus.HideFlameGlow();await Wait(.35);
                Check(controller.GetAnimationState().GetCurrent(1)?.GetAnimationName()=="_tracks/light_off",
                    "native extinguishing callback starts the independent light-off track");
                Check(controller.GetAnimationState().GetCurrent(0)!.GetAnimationName()==names[0]
                    && Mathf.IsEqualApprox(before,controller.GetAnimationState().GetCurrent(0)!.GetTrackTime()),
                    "turning off firelight preserves the base pose and its time");
                using var dark=GetViewport().GetTexture().GetImage();
                Check(!lit.SequenceEqual(dark.GetData()),"light-off actually removes the visible glow");
                dark.SavePng("D:/sts2mod/lingdawnpao/build/rest-native-unlit-capture.png");
            }
            var other=Fixture(0,original!,leopard:false);
            Check(!Apply(other) && Data(other)!.GetInstanceId()==original!.GetInstanceId(),"other characters keep their original visuals");
            other.Free();
            var missing=Fixture(0,original!,complete:false);
            Check(!Apply(missing) && Data(missing)!.GetInstanceId()==original!.GetInstanceId(),"missing UI anchors safely preserve the fallback");
            missing.Free();
            foreach(var avatar in avatars)avatar.QueueFree();
            await Wait(.1);
            Check(!Apply(focus),"stale callbacks cannot modify an exited rest-site avatar");
            Adapter.GetField("_skeleton",All)!.SetValue(null,null);data!.Dispose();original!.Dispose();
            GC.Collect();GC.WaitForPendingFinalizers();await Wait(.1);
            GD.Print($"PASS: {checks} native rest-site animation regression checks.");GetTree().Quit(0);
        }
        catch(Exception exception){GD.PrintErr(exception);GetTree().Quit(1);}
    }
}
