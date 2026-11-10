namespace XnaReborn.App.ViewModels;

/// <summary>
/// Collects values reported from any thread and hands them, in the order they arrived, to <c>deliver</c> in batches on the
/// thread of the context given at creation. Unlike <see cref="Progress{T}"/>, <see cref="Flush"/> can drain the rest at a
/// known point, so nothing arrives after the run has ended.
/// </summary>
/// <typeparam name="T">The reported type.</typeparam>
internal sealed class BuildRunSink<T> : IProgress<T>
{
    private readonly object _queueGate = new();
    private readonly object _flushGate = new();
    private readonly Queue<T> _queue = new();
    private readonly Action<IReadOnlyList<T>> _deliver;
    private readonly SynchronizationContext? _context;
    private bool _scheduled;

    /// <summary>Creates a sink.</summary>
    /// <param name="deliver">Receives each batch. Runs on the context's thread, or on the reporting thread when there is no context.</param>
    /// <param name="context">The context to deliver on (the UI thread's), or null to deliver on the reporting thread.</param>
    public BuildRunSink(Action<IReadOnlyList<T>> deliver, SynchronizationContext? context)
    {
        _deliver = deliver;
        _context = context;
    }

    /// <inheritdoc />
    public void Report(T value)
    {
        bool schedule;
        lock (_queueGate)
        {
            _queue.Enqueue(value);
            schedule = !_scheduled;
            _scheduled = true;
        }

        if (!schedule)
        {
            return;
        }

        if (_context is null)
        {
            Flush();
        }
        else
        {
            _context.Post(_ => Flush(), null);
        }
    }

    /// <summary>Delivers everything reported so far. Call it on the delivery thread when the run ends.</summary>
    public void Flush()
    {
        // One flush at a time, so two threads cannot deliver their batches out of order.
        lock (_flushGate)
        {
            List<T> batch;
            lock (_queueGate)
            {
                batch = [.. _queue];
                _queue.Clear();
                _scheduled = false;
            }

            if (batch.Count > 0)
            {
                _deliver(batch);
            }
        }
    }
}
