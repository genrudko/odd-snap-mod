using System.Diagnostics;

namespace OddSnap.Capture;

/// <summary>
/// Tracks active recording time while excluding every paused interval. The same
/// clock is shared by GIF and video recorders so frame scheduling, toolbar time,
/// maximum duration, and audio trimming all use one pause-aware timeline.
/// </summary>
internal sealed class RecordingPauseClock
{
    private readonly object _sync = new();
    private long _startTimestamp;
    private long _pauseStartedTimestamp;
    private long _accumulatedPausedTicks;
    private bool _isPaused;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
                return _isPaused;
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_sync)
            {
                if (_startTimestamp == 0)
                    return TimeSpan.Zero;

                long now = Stopwatch.GetTimestamp();
                long pausedTicks = _accumulatedPausedTicks;
                if (_isPaused && _pauseStartedTimestamp != 0)
                    pausedTicks += Math.Max(0, now - _pauseStartedTimestamp);

                long activeTicks = Math.Max(0, now - _startTimestamp - pausedTicks);
                return TimeSpan.FromSeconds(activeTicks / (double)Stopwatch.Frequency);
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _pauseStartedTimestamp = 0;
            _accumulatedPausedTicks = 0;
            _isPaused = false;
            Monitor.PulseAll(_sync);
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_startTimestamp == 0 || _isPaused)
                return;

            _pauseStartedTimestamp = Stopwatch.GetTimestamp();
            _isPaused = true;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (!_isPaused)
            {
                Monitor.PulseAll(_sync);
                return;
            }

            long now = Stopwatch.GetTimestamp();
            if (_pauseStartedTimestamp != 0)
                _accumulatedPausedTicks += Math.Max(0, now - _pauseStartedTimestamp);

            _pauseStartedTimestamp = 0;
            _isPaused = false;
            Monitor.PulseAll(_sync);
        }
    }

    public void WaitWhilePaused(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            while (_isPaused && !cancellationToken.IsCancellationRequested)
                Monitor.Wait(_sync, 100);
        }
    }
}
