namespace XnaReborn.App.ViewModels;

/// <summary>One entry of a drop-down on the Settings page: a value and the words the user sees.</summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <param name="Value">The value stored in the settings.</param>
/// <param name="Label">The text of the entry.</param>
public sealed record SettingChoice<T>(T Value, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}
