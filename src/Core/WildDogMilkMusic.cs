using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core;

internal sealed class WildDogMilkAudioSettings
{
    [JsonPropertyName("wild_dog_milk_enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("wild_dog_milk_volume")]
    public float Volume { get; set; } = 0.30f;
}

/// <summary>Local, combat-scoped music. Audio never owns or awaits a gameplay action.</summary>
public partial class WildDogMilkMusic : Node
{
    public const string AudioPath = "res://lingdawnpao/audio/wild_dog_milk.ogg";
    public const string SettingsPath = "user://lingdawnpao/audio.json";
    private static WildDogMilkMusic? _instance;
    private Player? _owner;
    private AudioStreamPlayer? _player;
    private WildDogMilkAudioSettings _settings = new();
    private string? _settingsFile;
    private DateTime _settingsStamp;
    private double _poll;
    private float _fade;
    private bool _ownsMusicVolume;

    internal static bool IsOverridingMusic => _instance != null
        && GodotObject.IsInstanceValid(_instance) && _instance._ownsMusicVolume;

    public static void TryStart(Player owner)
    {
        if (TestMode.IsOn || !LocalContext.IsMe(owner) || !AmbushZone.CanContinueCombat(owner)
            || !owner.Creature.HasPower<WildDogMilkPower>()) return;
        try
        {
            if (_instance != null && GodotObject.IsInstanceValid(_instance)) return;
            var room = NCombatRoom.Instance;
            if (room == null) return;
            var stream = ResourceLoader.Load<AudioStreamOggVorbis>(AudioPath);
            if (stream == null) { Log.Warn("野生狗奶：音乐资源缺失，继续游戏原配乐"); return; }
            stream.Loop = true;
            var music = new WildDogMilkMusic { _owner = owner, ProcessMode = ProcessModeEnum.Always };
            music._player = new AudioStreamPlayer { Stream = stream, VolumeDb = -80f, MaxPolyphony = 1 };
            music.AddChild(music._player);
            _instance = music;
            room.AddChild(music);
            music._settingsFile = ProjectSettings.GlobalizePath(SettingsPath);
            music.ReloadSettings();
            music.UpdatePlayback(0f);
        }
        catch (Exception exception)
        {
            Log.Warn($"野生狗奶：音乐启动失败，不影响卡牌结算（{exception}）");
            StopFor(owner);
        }
    }

    public static void StopFor(Player? owner)
    {
        if (TestMode.IsOn || owner == null || _instance == null || !GodotObject.IsInstanceValid(_instance)
            || _instance._owner != owner) return;
        _instance.StopAndFree();
    }

    public override void _Process(double delta)
    {
        try
        {
            if (_owner == null || !AmbushZone.CanContinueCombat(_owner)
                || !_owner.Creature.HasPower<WildDogMilkPower>()) { StopAndFree(); return; }
            _poll -= delta;
            if (_poll <= 0) { _poll = 0.5; ReloadSettings(); }
            UpdatePlayback((float)delta);
        }
        catch (Exception exception)
        {
            Log.Warn($"野生狗奶：音乐播放异常，恢复游戏配乐（{exception}）");
            StopAndFree();
        }
    }

    private void ReloadSettings()
    {
        if (_settingsFile == null) return;
        try
        {
            if (!System.IO.File.Exists(_settingsFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
                System.IO.File.WriteAllText(_settingsFile, JsonSerializer.Serialize(_settings,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            var stamp = System.IO.File.GetLastWriteTimeUtc(_settingsFile);
            if (stamp == _settingsStamp) return;
            _settingsStamp = stamp;
            var settings = JsonSerializer.Deserialize<WildDogMilkAudioSettings>(System.IO.File.ReadAllText(_settingsFile));
            if (settings != null) _settings = settings;
        }
        catch (Exception exception)
        {
            Log.Warn($"野生狗奶：音量配置无法读取，保留当前设置（{exception.Message}）");
        }
    }

    internal static float CalculateVolume(float master, float bgm, float track, float fade = 1f)
    {
        static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
        // The game's FMOD sliders use a squared response curve too.
        float m = Clamp(master), b = Clamp(bgm);
        return m * m * b * b * Clamp(track) * Clamp(fade);
    }

    private void UpdatePlayback(float delta)
    {
        if (_player == null) return;
        if (!_settings.Enabled)
        {
            _player.Stop();
            RestoreGameMusic();
            _fade = 0f;
            return;
        }
        var save = SaveManager.Instance;
        _fade = Math.Min(1f, _fade + delta / 0.4f);
        bool backgroundMute = save.PrefsSave.MuteInBackground && !DisplayServer.WindowIsFocused();
        float volume = backgroundMute ? 0f : CalculateVolume(save.SettingsSave.VolumeMaster,
            save.SettingsSave.VolumeBgm, _settings.Volume, _fade);
        _player.VolumeLinear = volume;
        if (!_player.Playing) _player.Play();
        if (!_ownsMusicVolume)
        {
            _ownsMusicVolume = true;
            NAudioManager.Instance?.SetBgmVol(0f);
        }
    }

    private void RestoreGameMusic()
    {
        if (!_ownsMusicVolume) return;
        _ownsMusicVolume = false;
        NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);
    }

    private void StopAndFree()
    {
        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _player.Stop();
            _player.Stream = null;
        }
        RestoreGameMusic();
        if (ReferenceEquals(_instance, this)) _instance = null;
        SetProcess(false);
        if (!IsQueuedForDeletion()) QueueFree();
    }

    public override void _ExitTree()
    {
        try { RestoreGameMusic(); }
        catch (Exception exception) { Log.Warn($"野生狗奶：退出时恢复配乐失败（{exception.Message}）"); }
        finally { if (ReferenceEquals(_instance, this)) _instance = null; }
    }
}

// Duck the original soundtrack without stopping its event or changing saved settings.
// Its normal track progression continues underneath, and is restored when this node exits.
[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetBgmVol))]
internal static class WildDogMilkMusicVolumePatch
{
    private static void Prefix(ref float volume)
    {
        if (WildDogMilkMusic.IsOverridingMusic) volume = 0f;
    }
}
