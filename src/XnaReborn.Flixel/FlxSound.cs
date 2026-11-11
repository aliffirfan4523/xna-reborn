using XnaReborn.As3;

namespace org.flixel;

/// <summary>One playing sound, behind an interface so the game can use <c>SoundEffectInstance</c> or <c>MediaPlayer</c> and tests a fake.</summary>
public interface IFlxSoundInstance : IDisposable
{
    /// <summary>Loudness from 0 to 1, after all mixing.</summary>
    double Volume { set; }

    /// <summary>True to start again at the end.</summary>
    bool Looped { set; }

    /// <summary>True while the sound is playing. A paused or finished sound is not playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Starts from the beginning.</summary>
    void Play();

    /// <summary>Holds the sound where it is.</summary>
    void Pause();

    /// <summary>Carries on after <see cref="Pause"/>.</summary>
    void Resume();

    /// <summary>Stops the sound and rewinds it.</summary>
    void Stop();
}

/// <summary>
/// A sound with volume, loop, pause and fade, like Flixel's <c>FlxSound</c>. The loudness that reaches the speakers is
/// <see cref="volume"/> times the fade times <see cref="FlxG.volume"/>, and 0 while <see cref="FlxG.mute"/> is on.
/// </summary>
public class FlxSound : FlxBasic
{
    private IFlxSoundInstance? _instance;
    private double _volume = 1;
    private bool _paused;
    private double _fadeFactor = 1;
    private double _fadeRate;
    private double _fadeTarget = 1;
    private bool _pauseAfterFade;

    /// <summary>The sound asset, or null before <see cref="loadEmbedded"/>.</summary>
    public AssetRef? asset { get; private set; }

    /// <summary>Whether the sound loops.</summary>
    public bool looped { get; set; }

    /// <summary>True means the sound is killed when it ends, so <see cref="FlxG.play"/> can reuse it.</summary>
    public bool autoDestroy { get; set; }

    /// <summary>True while the sound is playing.</summary>
    public bool playing { get; private set; }

    /// <summary>The sound's own loudness from 0 to 1.</summary>
    public double volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            Apply();
        }
    }

    /// <summary>Picks the sound asset to play. Stops what was playing.</summary>
    public FlxSound loadEmbedded(AsClass EmbeddedSound, bool Looped = false, bool AutoDestroy = false)
    {
        stop();
        _instance?.Dispose();
        asset = EmbeddedSound.Asset;
        _instance = asset is null ? null : FlxG.assets?.CreateSound(asset);
        looped = Looped;
        autoDestroy = AutoDestroy;
        _fadeFactor = 1;
        _fadeRate = 0;
        exists = true;
        alive = true;
        active = true;
        Apply();
        return this;
    }

    /// <summary>Plays the sound. A paused sound carries on. A playing one is left alone unless <paramref name="ForceRestart"/> is true.</summary>
    public void play(bool ForceRestart = false)
    {
        if (_instance is null)
        {
            return;
        }

        if (playing && !ForceRestart)
        {
            return;
        }

        Apply();
        _instance.Looped = looped;
        if (_paused && !ForceRestart)
        {
            _instance.Resume();
        }
        else
        {
            if (playing)
            {
                _instance.Stop();
            }

            _instance.Play();
        }

        playing = true;
        _paused = false;
    }

    /// <summary>Holds the sound. <see cref="play"/> carries on from there.</summary>
    public void pause()
    {
        if (_instance is null || !playing)
        {
            return;
        }

        _instance.Pause();
        playing = false;
        _paused = true;
    }

    /// <summary>Stops the sound and rewinds it.</summary>
    public void stop()
    {
        if (_instance is not null && (playing || _paused))
        {
            _instance.Stop();
        }

        playing = false;
        _paused = false;
        _fadeRate = 0;
    }

    /// <summary>Gets quieter over <paramref name="Seconds"/>, then stops (or pauses with <paramref name="PauseInstead"/>).</summary>
    public void fadeOut(double Seconds = 1, bool PauseInstead = false)
    {
        _pauseAfterFade = PauseInstead;
        StartFade(0, Seconds);
        if (_fadeRate == 0)
        {
            if (PauseInstead)
            {
                pause();
            }
            else
            {
                stop();
            }
        }
    }

    /// <summary>Starts playing from silence and gets louder over <paramref name="Seconds"/>.</summary>
    public void fadeIn(double Seconds = 1)
    {
        _fadeFactor = 0;
        play();
        StartFade(1, Seconds);
    }

    /// <summary>Fades, and notices when a sound that does not loop has ended.</summary>
    public override void update()
    {
        if (_fadeRate != 0)
        {
            _fadeFactor += _fadeRate * FlxG.elapsed;
            if ((_fadeRate > 0 && _fadeFactor >= _fadeTarget) || (_fadeRate < 0 && _fadeFactor <= _fadeTarget))
            {
                _fadeFactor = _fadeTarget;
                _fadeRate = 0;
                if (_fadeTarget == 0)
                {
                    if (_pauseAfterFade)
                    {
                        pause();
                    }
                    else
                    {
                        stop();
                    }
                }
            }
        }

        Apply();
        if (playing && !looped && _instance is { IsPlaying: false })
        {
            playing = false;
            if (autoDestroy)
            {
                kill();
            }
        }
    }

    /// <summary>Stops the sound and takes it out of play.</summary>
    public override void kill()
    {
        stop();
        base.kill();
    }

    /// <summary>Stops the sound and lets go of it.</summary>
    public override void destroy()
    {
        stop();
        _instance?.Dispose();
        _instance = null;
        base.destroy();
    }

    internal void Resume()
    {
        if (_paused)
        {
            play();
        }
    }

    internal void Apply()
    {
        if (_instance is not null)
        {
            _instance.Volume = FlxG.mute ? 0 : _volume * _fadeFactor * FlxG.volume;
        }
    }

    private void StartFade(double target, double seconds)
    {
        _fadeTarget = target;
        _fadeRate = (target - _fadeFactor) / Math.Max(seconds, 1e-6);
    }
}
