using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reporting;

/// <summary>Writes an <see cref="AnalysisReport"/> as indented camelCase JSON.</summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Converts the report to JSON with LF line endings and one trailing newline.</summary>
    /// <param name="report">The report to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(AnalysisReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var json = JsonSerializer.Serialize(report, _options);
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
}
