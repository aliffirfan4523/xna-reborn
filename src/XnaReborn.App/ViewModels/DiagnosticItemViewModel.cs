using XnaReborn.Runner.Diagnostics;

namespace XnaReborn.App.ViewModels;

/// <summary>One build error or warning inside a group.</summary>
public sealed class DiagnosticItemViewModel
{
    /// <summary>Creates the view model.</summary>
    /// <param name="diagnostic">The diagnostic from the runner.</param>
    public DiagnosticItemViewModel(BuildDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        Severity = diagnostic.Severity;
        Code = diagnostic.Code;
        Message = diagnostic.Message;
        Location = LocationOf(diagnostic);
    }

    /// <summary>Error, warning or info.</summary>
    public BuildDiagnosticSeverity Severity { get; }

    /// <summary>True for an error.</summary>
    public bool IsError => Severity == BuildDiagnosticSeverity.Error;

    /// <summary>True for a warning.</summary>
    public bool IsWarning => Severity == BuildDiagnosticSeverity.Warning;

    /// <summary>True for an info message.</summary>
    public bool IsInfo => Severity == BuildDiagnosticSeverity.Info;

    /// <summary>The code, such as CS0246.</summary>
    public string Code { get; }

    /// <summary>The message text.</summary>
    public string Message { get; }

    /// <summary>The file and line as <c>file:line</c>, the content asset path, or null when the diagnostic has neither.</summary>
    public string? Location { get; }

    /// <summary>True when <see cref="Location"/> has a value.</summary>
    public bool HasLocation => Location is not null;

    /// <summary>The code and location in one line, for the list.</summary>
    public string Detail => Location is null ? Code : Code + " at " + Location;

    private static string? LocationOf(BuildDiagnostic d)
    {
        if (!string.IsNullOrWhiteSpace(d.File))
        {
            return d.Line is { } line ? $"{d.File}:{line}" : d.File;
        }

        return string.IsNullOrWhiteSpace(d.AssetPath) ? null : d.AssetPath;
    }
}
