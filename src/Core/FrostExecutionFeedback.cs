using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace lingdawnpao.Core;

/// <summary>Local presentation only. Never changes a death result or adds an awaited game action.</summary>
internal static class FrostExecutionFeedback
{
    internal const string FreezePath = "res://lingdawnpao/audio/frost_crystallize.wav";
    internal const string CrackPath = "res://lingdawnpao/audio/frost_crack.wav";
    internal const string ShatterPath = "res://lingdawnpao/audio/frost_shatter.wav";
    private static AudioStream? _freeze, _crack, _shatter;
    private static readonly ulong[] LastPlayed = new ulong[3];
    private static readonly bool[] HasPlayed = new bool[3];

    internal static void Warmup()
    {
        try
        {
            _freeze = ResourceLoader.Load<AudioStream>(FreezePath);
            _crack = ResourceLoader.Load<AudioStream>(CrackPath);
            _shatter = ResourceLoader.Load<AudioStream>(ShatterPath);
        }
        catch (Exception exception) { Log.Warn($"冰冻处决：音效预热失败（{exception.Message}）"); }
    }

    internal static float Volume(float master, float sfx)
    {
        master = float.IsFinite(master) ? Math.Clamp(master, 0f, 1f) : 0f;
        sfx = float.IsFinite(sfx) ? Math.Clamp(sfx, 0f, 1f) : 0f;
        return master * master * sfx * sfx * 0.72f;
    }

    internal static void Freeze(Node parent) => Play(parent, 0);
    internal static void Crack(Node parent) => Play(parent, 1);
    internal static void Shatter(Node parent) => Play(parent, 2);

    private static void Play(Node parent, int stage)
    {
        if (TestMode.IsOn || !GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;
        try
        {
            ulong now = Time.GetTicksMsec();
            // AoE targets keep their own animation but share an impact sound/punch
            // when they fracture together, avoiding multiplied volume.
            if (HasPlayed[stage] && now - LastPlayed[stage] < 110) return;
            HasPlayed[stage] = true; LastPlayed[stage] = now;
            if (stage == 2) NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short, 12f);
            AudioStream? stream = stage == 2 ? _shatter : stage == 1 ? _crack : _freeze;
            if (stream == null) return;
            var save = SaveManager.Instance;
            float volume = save.PrefsSave.MuteInBackground && !DisplayServer.WindowIsFocused() ? 0f
                : Volume(save.SettingsSave.VolumeMaster, save.SettingsSave.VolumeSfx);
            if (volume <= 0f) return;
            var sound = new AudioStreamPlayer { Stream = stream, VolumeLinear = volume, MaxPolyphony = 1 };
            parent.AddChild(sound);
            sound.Finished += sound.QueueFree;
            sound.Play();
        }
        catch (Exception exception) { Log.Warn($"冰冻处决：声音或震屏不可用（{exception.Message}）"); }
    }

    internal static void DirectDamageDeath(Creature creature)
    {
        if (TestMode.IsOn || !creature.IsEnemy) return;
        try
        {
            var room = NCombatRoom.Instance;
            var node = room?.GetCreatureNode(creature);
            if (node == null) return;
            var impact = new FrostDamageImpact { Radius = Math.Clamp(node.Hitbox.Size.Length() * 0.23f, 40f, 115f) };
            room!.CombatVfxContainer.AddChild(impact);
            impact.GlobalPosition = node.Hitbox.GlobalPosition + node.Hitbox.Size * 0.5f;
            Shatter(impact);
        }
        catch (Exception exception) { Log.Warn($"冰冻致死：冰裂反馈不可用（{exception.Message}）"); }
    }
}

/// <summary>A short ice-crack impact layered over ordinary lethal-damage animation.</summary>
public partial class FrostDamageImpact : Node2D
{
    public float Radius { get; set; } = 65f;
    private float _age;
    public override void _Process(double delta)
    {
        _age += (float)delta;
        QueueRedraw();
        if (_age >= 1.05f) QueueFree();
    }
    public override void _Draw()
    {
        if (_age > 0.38f) return;
        float fade = MathF.Pow(1f - _age / 0.38f, 2f);
        float flash = Math.Clamp(1f - _age / 0.07f, 0f, 1f);
        DrawCircle(Vector2.Zero, Radius * 0.5f, new Color(0.72f, 0.95f, 1f, flash * 0.27f));
        for (int i = 0; i < 14; i++)
        {
            float angle = i * MathF.Tau / 14f + MathF.Sin(i * 17f) * 0.14f;
            var direction = Vector2.FromAngle(angle);
            var side = direction.Orthogonal();
            var point = direction * (Radius * 0.20f + _age * (190f + i % 4 * 35f));
            DrawColoredPolygon([point + direction * 15f, point + side * 3f,
                point - direction * 8f, point - side * 3f], new Color(0.76f, 0.96f, 1f, fade));
            if (_age < 0.11f)
                DrawLine(direction * Radius * 0.08f, direction * Radius * 0.8f,
                    new Color(0.83f, 0.98f, 1f, fade * 0.75f), 1.8f, true);
        }
    }
}
