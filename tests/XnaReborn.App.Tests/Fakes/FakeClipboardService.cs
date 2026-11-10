using XnaReborn.App.Services;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Remembers what was copied.</summary>
public sealed class FakeClipboardService : IClipboardService
{
    /// <summary>The last text copied, or null.</summary>
    public string? Text { get; private set; }

    /// <summary>Everything copied, in order.</summary>
    public List<string> History { get; } = [];

    /// <summary>Thrown by <see cref="SetTextAsync"/> when set, to test a locked clipboard.</summary>
    public Exception? Failure { get; set; }

    public Task SetTextAsync(string text)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Text = text;
        History.Add(text);
        return Task.CompletedTask;
    }
}
