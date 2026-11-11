using System.Text.Json;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules;

/// <summary>Reads a rule catalog from JSON and validates it against the embedded schema. An invalid catalog is never partly loaded.</summary>
public static class RuleCatalogLoader
{
    /// <summary>Parses and validates catalog JSON.</summary>
    /// <param name="json">The catalog text.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="RuleCatalogException">The text is not valid JSON or the catalog has problems. All problems are listed.</exception>
    public static RuleCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw Invalid([$"catalog /: invalid JSON: {ex.Message}"]);
        }

        using (document)
        {
            var errors = RuleCatalogValidator.Validate(document.RootElement);
            if (errors.Count > 0)
            {
                throw Invalid(errors);
            }

            try
            {
                return document.RootElement.Deserialize<RuleCatalog>(RuleCatalogJson.Options)
                    ?? throw Invalid(["catalog /: the document is empty."]);
            }
            catch (JsonException ex)
            {
                throw Invalid([$"catalog /: {ex.Message}"]);
            }
        }
    }

    /// <summary>Reads, parses, and validates a catalog file.</summary>
    /// <param name="path">Absolute path of the catalog file.</param>
    /// <param name="fileSystem">Read-only file system used to read the file.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="RuleCatalogException">The file is missing or unreadable, or the catalog is invalid.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task<RuleCatalog> LoadAsync(string path, IReadOnlyFileSystem fileSystem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Task.Run(() => Load(path, fileSystem, cancellationToken), cancellationToken);
    }

    /// <summary>Loads several catalog files and checks that no rule id appears in two of them.</summary>
    /// <param name="paths">Absolute paths of the catalog files.</param>
    /// <param name="fileSystem">Read-only file system used to read the files.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The catalogs, in the order of <paramref name="paths"/>.</returns>
    /// <exception cref="RuleCatalogException">A file is missing or invalid, or an id is defined twice.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<IReadOnlyList<RuleCatalog>> LoadAllAsync(IReadOnlyList<string> paths, IReadOnlyFileSystem fileSystem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(fileSystem);
        var catalogs = new List<RuleCatalog>();
        foreach (var path in paths)
        {
            catalogs.Add(await LoadAsync(path, fileSystem, cancellationToken).ConfigureAwait(false));
        }

        EnsureUniqueIds(catalogs);
        return catalogs;
    }

    /// <summary>Checks that no rule id appears in two of the catalogs.</summary>
    /// <param name="catalogs">Catalogs that will run together.</param>
    /// <exception cref="RuleCatalogException">An id is defined in more than one catalog. Every duplicate is listed.</exception>
    public static void EnsureUniqueIds(IReadOnlyList<RuleCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        var firstCatalogById = new Dictionary<string, int>(StringComparer.Ordinal);
        var errors = new List<string>();
        for (var i = 0; i < catalogs.Count; i++)
        {
            foreach (var rule in catalogs[i].Rules)
            {
                if (!firstCatalogById.TryAdd(rule.Id, i) && firstCatalogById[rule.Id] != i)
                {
                    errors.Add($"{rule.Id} catalog '{catalogs[i].Name}': duplicate rule id (also defined in catalog '{catalogs[firstCatalogById[rule.Id]].Name}').");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw Invalid(errors);
        }
    }

    private static RuleCatalogException Invalid(IReadOnlyList<string> errors) =>
        new($"Rule catalog is invalid: {errors.Count} problem(s).", errors);

    private static RuleCatalog Load(string path, IReadOnlyFileSystem fileSystem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string text;
        try
        {
            if (!fileSystem.FileExists(path))
            {
                throw new RuleCatalogException($"Rule catalog not found: {path}", [$"catalog /: file not found: {path}"]);
            }

            text = fileSystem.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RuleCatalogException($"Rule catalog could not be read: {path}", [$"catalog /: {ex.Message}"]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Parse(text);
    }
}
