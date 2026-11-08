# Tech conventions

- C# 12 on .NET 8. Nullable reference types enabled. Warnings as errors.
- Use Roslyn (`Microsoft.CodeAnalysis`) for source analysis and rewriting.
- Prefer immutable records for models. Use `async`/`await` with `CancellationToken` on all long operations.
- Rules are data (JSON catalog) first; add code only when a new match type is needed.
- Every rule needs a positive and a negative fixture test.
- Use xUnit and Verify snapshot tests. No tests that depend on network access.
- Format with `dotnet format`. Keep public APIs documented with XML comments.
