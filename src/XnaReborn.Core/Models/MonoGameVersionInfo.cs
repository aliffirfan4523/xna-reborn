namespace XnaReborn.Core.Models;

/// <summary>What the analyzer learned about the MonoGame version of a project.</summary>
/// <param name="Exact">Exact version text such as "3.7.0.1708", or null when only a band is known.</param>
/// <param name="Band">The version line or range.</param>
/// <param name="Confidence"><see cref="Confidence.High"/> for an exact version, <see cref="Confidence.Medium"/> for a band, <see cref="Confidence.Unknown"/> for no signal.</param>
public sealed record MonoGameVersionInfo(string? Exact, MonoGameBand Band, Confidence Confidence)
{
    /// <summary>No signal: no exact version, band <see cref="MonoGameBand.Unknown"/>.</summary>
    public static MonoGameVersionInfo Unknown { get; } = new(null, MonoGameBand.Unknown, Confidence.Unknown);
}
