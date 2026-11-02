using System.Reflection;
using Godot;
using HarmonyLib;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;

public partial class Runner : Node
{
    private int _checks;
    private static readonly Type Adapter = typeof(FrostbitePower).Assembly.GetType("lingdawnpao.Core.FrostExecutionVfx")!;
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static object? Call(string name, params object?[] args) => Adapter.GetMethod(name, All)!.Invoke(null, args);
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        _checks++;
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private IDisposable Scope(NCreatureVisuals visuals) => (IDisposable)Activator.CreateInstance(
        Adapter.GetNestedType("Scope", BindingFlags.NonPublic)!, All, null, [visuals], null)!;

    private (TestDoom Owner, SubViewport Viewport, Sprite2D Visual) Fixture(bool empty = false)
    {
        var owner = new TestDoom();
        var viewport = new SubViewport
        {
            Name = "Viewport", Size = new Vector2I(256, 320), TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            RenderTargetClearMode = SubViewport.ClearMode.Always
        };
        owner.AddChild(viewport);
        if (!empty)
        {
            using var image = Image.CreateEmpty(120, 160, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            for (int y = 12; y < 150; y++) for (int x = 10; x < 114; x++)
                if ((x - 60) * (x - 60) / 2800f + (y - 82) * (y - 82) / 4900f < 1)
                    image.SetPixel(x, y, new Color(0.7f, 0.55f, y / 160f, 1));
            viewport.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(image), Position = new Vector2(128, 220) });
        }
        // Same nested Mask offset as vfx_doom.tscn, to catch origin mistakes.
        var mask = new Node2D { Name = "Mask", Position = new Vector2(-960, -1089) };
        owner.AddChild(mask);
        var visual = new Sprite2D { Name = "Visual", Position = new Vector2(960, 1089), Texture = viewport.GetTexture() };
        mask.AddChild(visual);
        visual.Owner = owner;
        visual.UniqueNameInOwner = true;
        AddChild(owner);
        return (owner, viewport, visual);
    }

    private static Task Render(NDoomVfx owner) => (Task)typeof(NDoomVfx)
        .GetMethod("PlayVfxInternal", All)!.Invoke(owner, null)!;

    public override async void _Ready()
    {
        try
        {
            TestMode.IsOn = true;
            Check(ProjectSettings.LoadResourcePack("D:/sts2mod/lingdawnpao/build/lingdawnpao.pck"), "release pack loads");
            Check((bool)Call("Resolve")!, "native visual API resolves");
            Call("Warmup");
            Check(Adapter.GetField("_script", All)!.GetValue(null) is GDScript, "exported renderer and shaders load");
            var feedback = typeof(FrostbitePower).Assembly.GetType("lingdawnpao.Core.FrostExecutionFeedback")!;
            Check(ResourceLoader.Load<AudioStream>((string)feedback.GetField("FreezePath", All)!.GetRawConstantValue()!) is { },
                "crystallize sound is present in the release pack");
            Check(ResourceLoader.Load<AudioStream>((string)feedback.GetField("CrackPath", All)!.GetRawConstantValue()!) is { },
                "cracking sound is independent from the frozen pause");
            Check(ResourceLoader.Load<AudioStream>((string)feedback.GetField("ShatterPath", All)!.GetRawConstantValue()!) is { },
                "shatter sound is present in the release pack");
            float Volume(float master, float sfx) => (float)feedback.GetMethod("Volume", All)!.Invoke(null,[master,sfx])!;
            Check(Volume(0f,1f)==0 && Volume(1f,0f)==0, "either mute slider silences the custom sounds");
            Check(Math.Abs(Volume(.5f,.5f)-.045f)<.00001f, "sound uses both native squared volume sliders");
            Check(Volume(float.NaN,1f)==0 && Volume(-1f,1f)==0 && Volume(4f,2f)<=1f,
                "invalid sound preferences cannot cause excessive volume");
            var harmony = new Harmony("lingdawnpao.frost-regression");
            foreach (string name in new[] { "FrostExecutionCreatePatch", "FrostExecutionRenderPatch", "FrostExecutionOverlayPatch", "FrostExecutionSoundPatch" })
                Check(harmony.CreateClassProcessor(typeof(FrostbitePower).Assembly.GetType("lingdawnpao.Core." + name)!).Patch().Count > 0,
                    name + " binds");
            var a = new NCreatureVisuals();
            var b = new NCreatureVisuals();
            var ordinary = Fixture();
            Check(!(bool)Call("Owns", ordinary.Owner)!, "ordinary Doom is not overridden");
            using (Scope(a))
            {
                Call("Tag", a, false, ordinary.Owner);
                Check(!(bool)Call("Owns", ordinary.Owner)!, "player or prevented disappearance stays native");
                Call("Tag", b, true, ordinary.Owner);
                Check(!(bool)Call("Owns", ordinary.Owner)!, "different target cannot inherit frost scope");
                Call("Tag", a, true, ordinary.Owner);
                Check((bool)Call("Owns", ordinary.Owner)!, "matching frost owns this effect");
                using (Scope(b))
                    Check(!(bool)Adapter.GetProperty("SuppressDoomPresentation", All)!.GetValue(null)!, "nested scope starts clean");
                Check((bool)Adapter.GetProperty("SuppressDoomPresentation", All)!.GetValue(null)!, "nested scope restores parent");
            }
            Check(!(bool)Adapter.GetProperty("SuppressDoomPresentation", All)!.GetValue(null)!, "scope restored after call");
            Task playback = Render(ordinary.Owner); // Calls the real Harmony-patched private method.
            await Frames(6);
            Check(!playback.IsCompleted, "capture produces an animated effect rather than early cleanup");
            Check(!ordinary.Visual.Visible, "native capture display hidden only after shard setup");
            Check(ordinary.Viewport.RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled, "snapshot stops further live capture");
            var renderer = ordinary.Owner.GetChildren().OfType<Node2D>().First(n => n.GetScript().VariantType == Variant.Type.Object);
            Rect2 bounds = renderer.Get("bounds").AsRect2();
            Check(bounds.Size.X > 80 && bounds.Size.Y > 120, "opaque silhouette has its native size");
            Check(Mathf.Abs(renderer.Position.X) < 10 && renderer.Position.Y < -5 && renderer.Position.Y > -40,
                "cropped snapshot preserves original foot placement");
            Check(renderer.Get("fragments").AsGodotArray().Count >= 15, "captured silhouette builds shard polygons");
            await playback.WaitAsync(TimeSpan.FromSeconds(4));
            Check(ordinary.Owner.IsQueuedForDeletion(), "completed effect frees native viewport and captured body");
            await Frames(2);

            var interrupted = Fixture();
            using (Scope(a)) Call("Tag", a, true, interrupted.Owner);
            Task interruptedTask = Render(interrupted.Owner);
            interrupted.Owner.QueueFree();
            await Frames(2);
            Check(interruptedTask.IsCompletedSuccessfully, "leaving during capture releases death wait");

            var removedDuringPlay = Fixture();
            using (Scope(a)) Call("Tag", a, true, removedDuringPlay.Owner);
            Task removedTask = Render(removedDuringPlay.Owner);
            await Frames(6);
            removedDuringPlay.Owner.QueueFree();
            await Frames(2);
            Check(removedTask.IsCompletedSuccessfully, "leaving during shattering releases death wait");

            var empty = Fixture(true);
            using (Scope(a)) Call("Tag", a, true, empty.Owner);
            Task emptyTask = Render(empty.Owner);
            await emptyTask.WaitAsync(TimeSpan.FromSeconds(2));
            Check(empty.Owner.IsQueuedForDeletion(), "empty capture falls back and cannot hang death");
            await Frames(2);

            Task[] simultaneous = Enumerable.Range(0, 3).Select(_ =>
            {
                var fixture = Fixture();
                using (Scope(a)) Call("Tag", a, true, fixture.Owner);
                return Render(fixture.Owner);
            }).ToArray();
            await Task.WhenAll(simultaneous).WaitAsync(TimeSpan.FromSeconds(4));
            Check(simultaneous.All(t => t.IsCompletedSuccessfully), "multiple executions complete independently");
            await Frames(2);
            a.Free(); b.Free();
            GD.Print($"PASS: {_checks} frost integration checks ({typeof(NDoomVfx).Assembly.GetName().Version}).");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr(exception);
            GetTree().Quit(1);
        }
    }
}
