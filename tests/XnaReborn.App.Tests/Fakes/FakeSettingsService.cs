using XnaReborn.App.Services;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Keeps the settings in memory. Tests set <see cref="Current"/> before a page is opened, or read it after a change.</summary>
public sealed class FakeSettingsService : ISettingsService
{
    public event EventHandler? Changed;

    public AppSettings Current { get; set; } = new();

    public string? LoadProblem { get; set; }

    /// <summary>What <see cref="Save"/> returns, so a test can pretend the disk is full.</summary>
    public bool SaveSucceeds { get; set; } = true;

    /// <summary>How many times <see cref="Save"/> was called.</summary>
    public int SaveCount { get; private set; }

    public bool Save(AppSettings settings)
    {
        SaveCount++;
        var next = settings.Normalize();
        var changed = next != Current;
        Current = next;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return SaveSucceeds;
    }
}
