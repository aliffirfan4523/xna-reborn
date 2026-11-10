using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Tests.BuildRun;

public sealed class BuildRunSinkTests
{
    [Fact]
    public void Without_a_context_values_are_delivered_at_once_and_in_order()
    {
        var seen = new List<int>();
        var sink = new BuildRunSink<int>(batch => seen.AddRange(batch), null);

        sink.Report(1);
        sink.Report(2);
        sink.Report(3);

        Assert.Equal([1, 2, 3], seen);
    }

    [Fact]
    public void With_a_context_values_are_batched_into_one_post()
    {
        var context = new QueueContext();
        var batches = new List<int[]>();
        var sink = new BuildRunSink<int>(batch => batches.Add([.. batch]), context);

        sink.Report(1);
        sink.Report(2);
        sink.Report(3);

        Assert.Single(context.Posts);
        Assert.Empty(batches);
        context.RunAll();
        Assert.Equal([[1, 2, 3]], batches);

        sink.Report(4);
        Assert.Single(context.Posts);
    }

    [Fact]
    public void Flush_delivers_the_rest_and_a_late_post_adds_nothing()
    {
        var context = new QueueContext();
        var seen = new List<int>();
        var sink = new BuildRunSink<int>(batch => seen.AddRange(batch), context);

        sink.Report(1);
        sink.Report(2);
        sink.Flush();
        context.RunAll();

        Assert.Equal([1, 2], seen);
    }

    [Fact]
    public async Task Values_from_one_thread_arrive_in_order_while_another_thread_flushes()
    {
        var seen = new List<int>();
        var sink = new BuildRunSink<int>(batch => seen.AddRange(batch), null);

        var reporter = Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
            {
                sink.Report(i);
            }
        }, TestContext.Current.CancellationToken);
        var flusher = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                sink.Flush();
            }
        }, TestContext.Current.CancellationToken);
        await Task.WhenAll(reporter, flusher);
        sink.Flush();

        Assert.Equal(Enumerable.Range(0, 2000), seen);
    }

    private sealed class QueueContext : SynchronizationContext
    {
        public Queue<(SendOrPostCallback Callback, object? State)> Posts { get; } = new();

        public override void Post(SendOrPostCallback d, object? state) => Posts.Enqueue((d, state));

        public void RunAll()
        {
            while (Posts.Count > 0)
            {
                var (callback, state) = Posts.Dequeue();
                callback(state);
            }
        }
    }
}
