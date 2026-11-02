using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace lingdawnpao.Core;

/// <summary>Owns all waits for the frost renderer. Tree exit always releases death animation tasks.</summary>
public partial class FrostExecutionLifetime : Node
{
    private const double Duration = 1.65;
    private readonly TaskCompletionSource<bool> _capture = new();
    private readonly TaskCompletionSource _finished = new();
    private int _frames;
    private double _elapsed;
    private Node2D? _effect;
    private Sprite2D? _fallback;
    private bool _playing;

    internal Task<bool> CaptureReady => _capture.Task;
    internal Task Finished => _finished.Task;

    internal void Play(Node2D effect)
    {
        _effect = effect;
        _elapsed = 0;
        _playing = true;
        SetProcess(true);
        effect.Call("seek", 0.0);
    }

    internal void Fade(Sprite2D visual)
    {
        _effect = null;
        _fallback = visual;
        _elapsed = 0;
        _playing = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        try
        {
            if (!_playing)
            {
                if (++_frames >= 3)
                {
                    SetProcess(false);
                    _capture.TrySetResult(true);
                }
                return;
            }
            _elapsed += delta;
            if (GodotObject.IsInstanceValid(_effect)) _effect!.Call("seek", _elapsed);
            if (GodotObject.IsInstanceValid(_fallback))
                _fallback!.Modulate = new Color(1, 1, 1, Mathf.Clamp(1f - (float)_elapsed / 0.25f, 0, 1));
            if (_elapsed >= (_fallback != null ? 0.25 : Duration)) Complete();
        }
        catch (Exception exception)
        {
            Log.Warn($"冰冻处决：特效驱动异常，释放等待（{exception.Message}）");
            Complete();
        }
    }

    private void Complete()
    {
        SetProcess(false);
        _playing = false;
        _finished.TrySetResult();
    }

    public override void _ExitTree()
    {
        _capture.TrySetResult(false);
        _finished.TrySetResult();
    }
}
