using XnaReborn.As3;

namespace org.flixel;

/// <summary>
/// Calls a function after a time, once or several times, like Flixel's <c>FlxTimer</c>.
/// Written for XNA Reborn against the public Flixel 2.x API; see THIRD-PARTY-NOTICES.md for the Flixel (MIT) notice.
/// </summary>
public class FlxTimer
{
    /// <summary>The timers that are running. <see cref="FlxG.step"/> updates it every frame.</summary>
    public static TimerManager manager = new();

    private double _timeCounter;
    private int _loopsCounter;

    /// <summary>Seconds between calls.</summary>
    public double time;

    /// <summary>How many times the callback runs. 0 means forever.</summary>
    public int loops;

    /// <summary>True when the timer is stopped or has run all its loops.</summary>
    public bool finished = true;

    /// <summary>True while the timer is paused. A paused timer does not count.</summary>
    public bool paused;

    /// <summary>The function called each time the timer fires. It gets this timer as its argument.</summary>
    public AsFunction? callback;

    /// <summary>Starts (or restarts) the timer and adds it to <see cref="manager"/>.</summary>
    /// <param name="Time">Seconds between calls.</param>
    /// <param name="Loops">How many times to fire. 0 means forever.</param>
    /// <param name="Callback">What to call each time.</param>
    public FlxTimer start(double Time = 1, int Loops = 1, AsFunction? Callback = null)
    {
        manager.add(this);
        time = Time;
        loops = Loops;
        callback = Callback;
        _timeCounter = 0;
        _loopsCounter = 0;
        paused = false;
        finished = false;
        return this;
    }

    /// <summary>Stops the timer and takes it out of <see cref="manager"/>.</summary>
    public void stop()
    {
        finished = true;
        manager.remove(this);
    }

    /// <summary>Seconds left until the next call.</summary>
    public double timeLeft => time - _timeCounter;

    /// <summary>How many calls are left. 0 for a timer that runs forever.</summary>
    public int loopsLeft => loops - _loopsCounter;

    /// <summary>How many times the callback has run.</summary>
    public int elapsedLoops => _loopsCounter;

    /// <summary>Progress through the current wait, from 0 to 1.</summary>
    public double progress => time > 0 ? _timeCounter / time : 0;

    /// <summary>Counts time forward. The manager calls it every frame.</summary>
    public void update()
    {
        _timeCounter += FlxG.elapsed;
        while (_timeCounter >= time && !paused && !finished)
        {
            _timeCounter -= time;
            _loopsCounter++;
            callback?.Call(this);
            if (loops > 0 && _loopsCounter >= loops)
            {
                stop();
            }

            if (time <= 0)
            {
                break; // shortcut: a zero wait fires once per frame instead of looping forever; Flixel would hang.
            }
        }
    }

    /// <summary>Keeps the running timers and updates them each frame.</summary>
    public sealed class TimerManager
    {
        private readonly List<FlxTimer> _timers = new();

        /// <summary>Number of timers that are running.</summary>
        public int count => _timers.Count;

        /// <summary>Adds a timer once.</summary>
        public void add(FlxTimer timer)
        {
            if (!_timers.Contains(timer))
            {
                _timers.Add(timer);
            }
        }

        /// <summary>Takes a timer out.</summary>
        public void remove(FlxTimer timer) => _timers.Remove(timer);

        /// <summary>Drops every timer without calling it.</summary>
        public void clear() => _timers.Clear();

        /// <summary>Updates every timer that is not paused.</summary>
        public void update()
        {
            foreach (var timer in _timers.ToList())
            {
                if (!timer.paused && !timer.finished)
                {
                    timer.update();
                }
            }
        }
    }
}
