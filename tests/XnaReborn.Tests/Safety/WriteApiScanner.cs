using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Tests.Safety;

/// <summary>
/// Syntax check that analyzer code cannot write to disk. Only <c>ReadOnlyFileSystem.cs</c> may touch
/// <c>File</c>, <c>Directory</c>, or <c>FileStream</c>, and only for reading.
/// </summary>
public static class WriteApiScanner
{
    private const string _allowedFileName = "ReadOnlyFileSystem.cs";

    private static readonly string[] _bannedTypes =
    [
        "StreamWriter", "BinaryWriter", "FileInfo", "DirectoryInfo", "FileSystemWatcher",
        "HttpClient", "WebClient", "Socket", "TcpClient", "UdpClient",
    ];

    private static readonly string[] _allowedFileMethods = ["Exists"];
    private static readonly string[] _allowedDirectoryMethods = ["Exists", "EnumerateFiles", "EnumerateDirectories"];

    /// <summary>Finds uses of write-capable or network APIs in one source file.</summary>
    /// <param name="source">C# source text.</param>
    /// <param name="fileName">File name, used for the allow-list and in messages.</param>
    /// <returns>One message per violation, with the line number.</returns>
    public static IReadOnlyList<string> FindViolations(string source, string fileName)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var isAllowedFile = string.Equals(fileName, _allowedFileName, StringComparison.Ordinal);
        var violations = new List<string>();

        void Add(SyntaxNode node, string message) =>
            violations.Add($"{fileName}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {message}");

        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var owner = access.Expression.ToString().Replace("System.IO.", string.Empty, StringComparison.Ordinal);
            var member = access.Name.Identifier.Text;
            switch (owner)
            {
                case "File" when !isAllowedFile || !_allowedFileMethods.Contains(member):
                    Add(access, $"File.{member} is not allowed here");
                    break;
                case "Directory" when !isAllowedFile || !_allowedDirectoryMethods.Contains(member):
                    Add(access, $"Directory.{member} is not allowed here");
                    break;
                case "FileMode" when member != "Open":
                    Add(access, $"FileMode.{member} can create or change files");
                    break;
                case "FileAccess" when member != "Read":
                    Add(access, $"FileAccess.{member} allows writing");
                    break;
                case "FileOptions" when member == "DeleteOnClose":
                    Add(access, "FileOptions.DeleteOnClose deletes files");
                    break;
                case "Path" when member == "GetTempFileName":
                    Add(access, "Path.GetTempFileName creates a file");
                    break;
                case "Process":
                    Add(access, $"Process.{member} starts or controls processes");
                    break;
            }
        }

        foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            var typeName = creation is ObjectCreationExpressionSyntax typed ? typed.Type.ToString() : string.Empty;
            if (typeName.EndsWith("FileStream", StringComparison.Ordinal) && !isAllowedFile)
            {
                Add(creation, "FileStream is only allowed in ReadOnlyFileSystem.cs");
            }
        }

        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (_bannedTypes.Contains(identifier.Identifier.Text) && identifier.Parent is not NameColonSyntax)
            {
                Add(identifier, $"{identifier.Identifier.Text} is not allowed in the analyzer");
            }
        }

        return violations;
    }
}
