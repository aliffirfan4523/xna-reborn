using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public sealed class FlxSoundTests
{
    private static AsClass Jump => AsClass.FromAsset("snd/jump", AssetKind.Sound);

    private static AsClass Theme => AsClass.FromAsset("snd/theme", AssetKind.Song);

    [Fact]
    public void Play_starts_a_sound_at_the_set_volume_and_loop_flag()
    {
        var host = new FlxTestHost();
        var sound = FlxG.play(Jump, 0.5, true);
        var fake = Assert.Single(host.Sounds);
        Assert.True(fake.IsPlaying);
        Assert.True(fake.Looped);
        Assert.Equal(0.5, fake.Volume);
        Assert.True(sound.playing);
        Assert.Contains(sound, FlxG.sounds.members);
    }

    [Fact]
    public void Master_volume_and_mute_scale_every_sound()
    {
        var host = new FlxTestHost();
        FlxG.play(Jump, 0.5);
        FlxG.volume = 0.5;
        Assert.Equal(0.25, host.Sounds[0].Volume);
        FlxG.mute = true;
        Assert.Equal(0, host.Sounds[0].Volume);
        FlxG.mute = false;
        Assert.Equal(0.25, host.Sounds[0].Volume);
        FlxG.volume = 7;
        Assert.Equal(1, FlxG.volume);
    }

    [Fact]
    public void Pause_holds_and_play_carries_on_without_restarting()
    {
        var host = new FlxTestHost();
        var sound = FlxG.play(Jump);
        sound.pause();
        var fake = host.Sounds[0];
        Assert.False(sound.playing);
        Assert.True(fake.Paused);
        sound.play();
        Assert.True(fake.IsPlaying);
        Assert.Single(host.Played);
        sound.play();
        Assert.Single(host.Played);
        sound.play(true);
        Assert.Equal(2, host.Played.Count);
    }

    [Fact]
    public void A_sound_that_ends_is_killed_and_the_next_play_reuses_it()
    {
        var host = new FlxTestHost();
        var first = FlxG.play(Jump);
        host.Sounds[0].IsPlaying = false;
        host.Frame();
        Assert.False(first.exists);
        var second = FlxG.play(Jump);
        Assert.Same(first, second);
        Assert.Equal(1, FlxG.sounds.length);
    }

    [Fact]
    public void A_looping_sound_is_not_killed_when_the_instance_reports_stopped()
    {
        var host = new FlxTestHost();
        var sound = FlxG.play(Jump, 1, true);
        host.Frame();
        Assert.True(sound.exists);
        Assert.True(sound.playing);
    }

    [Fact]
    public void Fade_out_lowers_the_volume_then_stops()
    {
        var host = new FlxTestHost(step: 0.25);
        var sound = FlxG.play(Jump, 1, true);
        sound.fadeOut(1);
        host.Frame(2);
        Assert.Equal(0.5, host.Sounds[0].Volume, 3);
        Assert.True(sound.playing);
        host.Frame(2);
        Assert.Equal(0, host.Sounds[0].Volume, 3);
        Assert.False(sound.playing);
        Assert.False(host.Sounds[0].IsPlaying);
    }

    [Fact]
    public void Fade_out_can_pause_instead_of_stopping()
    {
        var host = new FlxTestHost(step: 0.5);
        var sound = FlxG.play(Jump, 1, true);
        sound.fadeOut(1, true);
        host.Frame(2);
        Assert.False(sound.playing);
        Assert.True(host.Sounds[0].Paused);
    }

    [Fact]
    public void Fade_in_starts_silent_and_rises_to_the_set_volume()
    {
        var host = new FlxTestHost(step: 0.25);
        var sound = new FlxSound().loadEmbedded(Jump, true);
        FlxG.sounds.add(sound);
        sound.volume = 0.8;
        sound.fadeIn(1);
        Assert.Equal(0, host.Sounds[0].Volume);
        Assert.True(host.Sounds[0].IsPlaying);
        host.Frame(2);
        Assert.Equal(0.4, host.Sounds[0].Volume, 3);
        host.Frame(2);
        Assert.Equal(0.8, host.Sounds[0].Volume, 3);
    }

    [Fact]
    public void Play_music_loops_one_song_and_replaces_the_one_before()
    {
        var host = new FlxTestHost();
        FlxG.playMusic(Theme, 0.6);
        Assert.NotNull(FlxG.music);
        Assert.True(host.Sounds[0].Looped);
        Assert.Equal(0.6, host.Sounds[0].Volume);
        var music = FlxG.music;
        FlxG.playMusic(AsClass.FromAsset("snd/boss", AssetKind.Song));
        Assert.Same(music, FlxG.music);
        Assert.True(host.Sounds[0].Disposed);
        Assert.Equal("snd/boss", host.Sounds[1].Name);
        Assert.True(host.Sounds[1].IsPlaying);
    }

    [Fact]
    public void Pause_and_resume_sounds_reach_the_music_and_only_the_paused_sounds()
    {
        var host = new FlxTestHost();
        FlxG.playMusic(Theme);
        FlxG.play(Jump, 1, true);
        var stopped = FlxG.play(Jump, 1, true);
        stopped.stop();
        FlxG.pauseSounds();
        Assert.All(host.Sounds, s => Assert.False(s.IsPlaying));
        FlxG.resumeSounds();
        Assert.True(host.Sounds[0].IsPlaying);
        Assert.True(host.Sounds[1].IsPlaying);
        Assert.False(host.Sounds[2].IsPlaying);
    }

    [Fact]
    public void Switching_state_drops_sounds_but_keeps_the_music()
    {
        var host = new FlxTestHost();
        host.Start(new FlxState());
        host.Frame();
        FlxG.playMusic(Theme);
        FlxG.play(Jump, 1, true);
        FlxG.switchState(new FlxState());
        host.Frame();
        Assert.True(host.Sounds[0].IsPlaying);
        Assert.False(host.Sounds[1].IsPlaying);
        Assert.True(host.Sounds[1].Disposed);
        Assert.Equal(0, FlxG.sounds.length);
    }

    [Fact]
    public void Playing_something_that_is_not_an_asset_does_nothing()
    {
        var host = new FlxTestHost();
        var sound = FlxG.play(AsClass.Of<FlxSprite>());
        Assert.False(sound.playing);
        Assert.Empty(host.Sounds);
    }
}
