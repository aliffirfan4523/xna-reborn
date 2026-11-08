using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>The platform an XNA project targets.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum XnaPlatform
{
    /// <summary>Not stated in the project file.</summary>
    Unknown,

    /// <summary>Windows desktop.</summary>
    Windows,

    /// <summary>Xbox 360.</summary>
    Xbox360,

    /// <summary>Zune.</summary>
    Zune,

    /// <summary>Windows Phone.</summary>
    WindowsPhone,
}
