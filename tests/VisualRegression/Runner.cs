using System.Reflection;
using Godot;
using HarmonyLib;
using lingdawnpao.Core;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.TestSupport;
public partial class Runner : Node
{
    private int checks;
    private void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    public override async void _Ready()
    {
        try
        {
            TestMode.IsOn = true;
            var visuals = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.AmbushCardVisuals")!;
            object? Call(string name, params object?[] args) => visuals.GetMethod(name)!.Invoke(null, args);
            ModelDb.Inject(typeof(ChainClaw));
            var model = ModelDb.Card<ChainClaw>().ToMutable();
            var card = new TestCard();
            var body = new Control();
            card.AddChild(body);
            typeof(NCard).GetProperty(nameof(NCard.Body))!.SetValue(card, body);
            typeof(NCard).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(card, model);
            var poolPatch = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.NCard_OnReturnedFromPool_ForceResetPatch")!;
            var harmony = new Harmony("lingdawnpao.visual-regression");
            harmony.CreateClassProcessor(poolPatch).Patch();
            foreach (string name in new[] { "NCard_OnFreedToPool_CancelAmbushAnimationPatch", "NPlayerHand_Add_RestoreAmbushCardPatch", "WildDogMilkManualEntryPatch", "WildDogMilkVisualPatch", "WildDogMilkMusicVolumePatch", "CharacterModel_GenerateAnimator_LingdawnpaoPatch" })
            {
                var patch = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core." + name)!;
                Check(harmony.CreateClassProcessor(patch).Patch().Count > 0, name + " must bind to the actual game method");
            }
            card.Modulate = new Color(0, 0, 0, 0);
            card.SelfModulate = new Color(1, 1, 1, 0);
            card.Visible = false;
            card.Scale = new Vector2(0.01f, 0.01f);
            body.Modulate = new Color(0, 0, 0, 0);
            body.SelfModulate = new Color(1, 1, 1, 0);
            body.Visible = false;
            body.Scale = new Vector2(0.01f, 0.01f);
            card.OnReturnedFromPool(); // Real patched lifecycle, while IsNodeReady is false.
            Check(card.Modulate == Colors.White && card.SelfModulate == Colors.White && card.Visible && card.Scale == Vector2.One,
                "pooled card root must be fully visible");
            Check(body.Modulate == Colors.White && body.SelfModulate == Colors.White && body.Visible && body.Scale == Vector2.One,
                "pooled card body must be fully visible");
            AddChild(card);
            bool staleCallbackRan = false;
            var oldTween = card.CreateTween();
            int oldEpoch = (int)Call("TrackAnimation", card, oldTween)!;
            oldTween.TweenProperty(card, "modulate", new Color(0, 0, 0, 0), 0.03);
            oldTween.TweenCallback(Callable.From(() => staleCallbackRan = true));
            Call("RestoreForHand", card);
            for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!staleCallbackRan && card.Modulate == Colors.White, "old burial animation must not fade a returned hand card");
            Check(!(bool)Call("CanComplete", card, model, oldEpoch)!, "stale callback cannot free a reused card node");
            bool currentCallbackRan = false;
            var freshTween = card.CreateTween();
            int freshEpoch = (int)Call("TrackAnimation", card, freshTween)!;
            freshTween.TweenInterval(0.03);
            freshTween.TweenCallback(Callable.From(() => currentCallbackRan = (bool)Call("CanComplete", card, model, freshEpoch)!));
            for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(currentCallbackRan, "the current owner's normal animation callback must still work");
            Call("CancelOwnedAnimation", card);
            Check(!(bool)Call("CanComplete", card, model, freshEpoch)!, "pool cleanup invalidates active animation ownership");
            card.Free();
            Check(ProjectSettings.LoadResourcePack("D:/sts2mod/lingdawnpao/build/lingdawnpao.pck"),
                "the audio release pack loads");
            var audio = ResourceLoader.Load<AudioStreamOggVorbis>(WildDogMilkMusic.AudioPath);
            Check(audio != null && audio.GetLength() > 318 && audio.GetLength() < 320,
                "the converted full-length music is decodable from the exported pack");
            var music = new WildDogMilkMusic();
            var audioPlayer = new AudioStreamPlayer { Stream = audio };
            music.AddChild(audioPlayer);
            AddChild(music);
            music.SetProcess(false);
            var musicType = typeof(WildDogMilkMusic);
            musicType.GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(music, audioPlayer);
            musicType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, music);
            musicType.GetField("_ownsMusicVolume", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(music, true);
            audioPlayer.Play();
            Check(audioPlayer.Playing, "the imported stream can start on a native Godot audio player");
            var volumePatch = typeof(Lingdawnpao).Assembly.GetType("lingdawnpao.Core.WildDogMilkMusicVolumePatch")!;
            var volumePrefix = volumePatch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
            object?[] mutedVolume = [0.7f];
            volumePrefix.Invoke(null, mutedVolume);
            Check((float)mutedVolume[0]! == 0, "original BGM is ducked while custom music owns playback");
            musicType.GetMethod("StopAndFree", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(music, null);
            object?[] restoredVolume = [0.7f];
            volumePrefix.Invoke(null, restoredVolume);
            Check(!audioPlayer.Playing && music.IsQueuedForDeletion() && (float)restoredVolume[0]! == 0.7f,
                "stopping combat music stops its player, frees the node and releases the original BGM volume");
            for (int i = 0; i < 2; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            audio?.Dispose();
            // Let the audio mixer acknowledge Stop before shutting down the test host.
            await Task.Delay(200);
            GD.Print($"PASS: {checks} native Godot card-visual regression checks.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
    }
}
