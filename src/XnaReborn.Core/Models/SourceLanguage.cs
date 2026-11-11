using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>The language of a project's source files. Serialized as "csharp" or "vb".</summary>
[JsonConverter(typeof(SourceLanguageJsonConverter))]
public enum SourceLanguage
{
    /// <summary>C#. The default.</summary>
    CSharp,

    /// <summary>Visual Basic.</summary>
    VisualBasic,
}
