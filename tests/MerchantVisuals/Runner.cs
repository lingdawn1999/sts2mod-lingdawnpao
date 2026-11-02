using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.TestSupport;

public partial class Runner : Node2D
{
    private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private int checks;
    private static readonly Type Adapter=typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.LingdawnpaoMerchantVisuals")!;
    private static object? Call(string name,params object?[] values)=>Adapter.GetMethod(name,All)!.Invoke(null,values);
    private void Check(bool result,string label){if(!result)throw new Exception(label);checks++;}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private Player Player(ulong id,CharacterModel character)
    {
        var p=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        typeof(Player).GetField("<NetId>k__BackingField",All)!.SetValue(p,id);
        typeof(Player).GetField("<Character>k__BackingField",All)!.SetValue(p,character);
        typeof(Player).GetField("<Creature>k__BackingField",All)!.SetValue(p,new Creature(p,72,72));
        return p;
    }
    private NMerchantCharacter Avatar(Resource original,Vector2 position)
    {
        var avatar=new NMerchantCharacter{Position=position};
        var sprite=ClassDB.Instantiate("SpineSprite").As<Node2D>();
        sprite.Set("skeleton_data_res",original);sprite.Scale=Vector2.One*.47f;
        avatar.AddChild(sprite);AddChild(avatar);return avatar;
    }
    private static Resource? Data(NMerchantCharacter avatar)=>avatar.GetChild(0).Get("skeleton_data_res").As<Resource>();
    private static MegaSprite Controller(NMerchantCharacter avatar)=>new(Variant.From(avatar.GetChild(0)));
    public override async void _Ready()
    {
        try
        {
            TestMode.IsOn=true;
            Check(ClassDB.ClassExists("SpineSprite"),"native Spine extension loads");
            Check(ProjectSettings.LoadResourcePack("D:/sts2mod/lingdawnpao/build/lingdawnpao.pck"),"current release pack loads");
            foreach(var type in new[]{typeof(Lingdawnpao),typeof(MegaCrit.Sts2.Core.Models.Characters.Ironclad),typeof(MegaCrit.Sts2.Core.Models.Characters.Silent)})ModelDb.Inject(type);
            string shopPath=(string)Adapter.GetField("SkeletonPath",All)!.GetRawConstantValue()!;
            var shop=ResourceLoader.Load<Resource>(shopPath);
            var original=ResourceLoader.Load<Resource>("res://animations/characters/lingdawnpao/v5_hurt_death/snow_leopard_v5_skel_data.tres");
            Check(shop!=null && original!=null,"shop data, atlas page and original battle resource load");
            var players=new[]{Player(2801,ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>()),
                Player(2802,ModelDb.Character<Lingdawnpao>()),Player(2803,ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Silent>()),
                Player(2804,ModelDb.Character<Lingdawnpao>())};
            var avatars=new[]{Avatar(original!,new Vector2(140,520)),Avatar(original!,new Vector2(470,520)),
                Avatar(original!,new Vector2(150,280)),Avatar(original!,new Vector2(650,290))};
            avatars[3].Modulate=new Color(.5f,.5f,.5f);avatars[3].Scale=Vector2.One*.9f;
            var positions=avatars.Select(a=>a.Position).ToArray();
            await Wait(.1);
            var patch=typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.NMerchantRoom_LingdawnpaoVisualsPatch")!;
            Check(new Harmony("lingdawnpao.merchant-regression").CreateClassProcessor(patch).Patch().Count>0,
                "shop patch binds to the actual native room method and player list");
            Call("Apply",players,avatars);
            await Wait(.15);
            Check(Data(avatars[0])!.GetInstanceId()==original!.GetInstanceId() && Data(avatars[2])!.GetInstanceId()==original.GetInstanceId(),
                "other players keep their original skeletons");
            Check(Data(avatars[1])!.GetInstanceId()==shop!.GetInstanceId() && Data(avatars[3])!.GetInstanceId()==shop.GetInstanceId(),
                "both snow leopards in a mixed co-op list receive the shop pose");
            Check(avatars.Select(a=>a.Position).SequenceEqual(positions) && avatars[3].Scale==Vector2.One*.9f
                && avatars[3].Modulate==new Color(.5f,.5f,.5f),"co-op placement, parent scale and row tint are preserved");
            Check(avatars[1].GetChild<Node2D>(0).Scale==Vector2.One*.64f,"shop pose uses its intended native display size");
            var controller=Controller(avatars[1]);
            foreach(string name in new[]{"relaxed_loop","idle_loop","shop_idle","die"})Check(controller.HasAnimation(name),"shop has "+name);
            Check(controller.GetAnimationState().GetCurrentAnimationName()=="relaxed_loop","initialization starts the native merchant loop");
            await Wait(.4);
            float before=controller.GetAnimationState().GetCurrent(0)!.GetTrackTime();
            Call("Apply",players,avatars);
            Check(Mathf.IsEqualApprox(before,controller.GetAnimationState().GetCurrent(0)!.GetTrackTime()),
                "reapplying the room visual does not restart breathing or purse motion");
            var missing=new NMerchantCharacter();
            Check(!(bool)Call("TryApply",players[1],missing)!,"a missing placeholder sprite safely keeps the native fallback");
            missing.Free();
            Check(!(bool)Call("TryApply",players[0],avatars[1])!,"a different character cannot inherit the replacement");
            foreach(var a in avatars)a.Visible=false;
            avatars[1].Visible=true;
            await Wait(.05);
            using(var capture=GetViewport().GetTexture().GetImage())capture.SavePng("D:/sts2mod/lingdawnpao/build/merchant-native-capture.png");
            avatars[1].PlayAnimation("die");
            Check(controller.GetAnimationState().GetCurrentAnimationName()=="die","native shop game-over callback can play death");
            await Wait(1.95);
            Check(controller.GetAnimationState().GetCurrent(0)!.IsComplete(),"shop death reaches its finite end");
            using(var capture=GetViewport().GetTexture().GetImage())
            {
                var pixels=capture.GetData();await Wait(.2);
                using var held=GetViewport().GetTexture().GetImage();
                Check(pixels.SequenceEqual(held.GetData()),"shop death holds still without restarting the loop");
            }
            avatars[1].PlayAnimation("relaxed_loop",true);await Wait(.1);
            Check(controller.GetAnimationState().GetCurrentAnimationName()=="relaxed_loop","a fresh loop clears the death pose");
            foreach(var a in avatars)a.QueueFree();
            await Wait(.1);
            Check(!(bool)Call("TryApply",players[1],avatars[1])!,"departed shop avatars cannot be modified by stale callbacks");
            typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.LingdawnpaoMerchantVisuals")!.GetField("_skeleton",All)!.SetValue(null,null);
            shop.Dispose();original.Dispose();
            GC.Collect();GC.WaitForPendingFinalizers();await Wait(.1);
            GD.Print($"PASS: {checks} native merchant animation regression checks.");GetTree().Quit(0);
        }
        catch(Exception exception){GD.PrintErr(exception);GetTree().Quit(1);}
    }
}
