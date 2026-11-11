using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Shims;

namespace XnaReborn.Tests.Device;

/// <summary>
/// Compiles the generated XnaReborn.Compat sources (with the device part) against the real MonoGame assembly from the NuGet cache and
/// loads the result, so the pure mapping classes can be tested without a window. The tests reach the internal types by reflection.
/// </summary>
internal static class DeviceCompat
{
    private static readonly Lazy<Assembly> _compat = new(Build);

    private static Assembly? _monoGame;

    internal static Assembly Assembly => _compat.Value;

    internal static Assembly MonoGame
    {
        get
        {
            _ = Assembly;
            return _monoGame!;
        }
    }

    internal static IReadOnlyList<string> CompileProblems(DeviceControlsModel? controls = null)
    {
        var path = MonoGameFactAttribute.Path()!;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(path)
            .Select(p => MetadataReference.CreateFromFile(p));
        var sources = new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, includeDevice: true, controls)
            .Where(c => c.RelativePath.EndsWith(".cs", StringComparison.Ordinal))
            .Select(c => CSharpSyntaxTree.ParseText(c.NewText!, new CSharpParseOptions(LanguageVersion.CSharp12), c.RelativePath));
        var compilation = CSharpCompilation.Create(
            "XnaReborn.Compat.DeviceTestBuild",
            sources,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        return emitted.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
    }

    internal static Type CompatType(string name) => Assembly.GetType(name, throwOnError: true)!;

    internal static Type MonoGameType(string name) => MonoGame.GetType(name, throwOnError: true)!;

    internal static object Vector3(float x, float y, float z) => Activator.CreateInstance(MonoGameType("Microsoft.Xna.Framework.Vector3"), x, y, z)!;

    internal static object Vector2(float x, float y) => Activator.CreateInstance(MonoGameType("Microsoft.Xna.Framework.Vector2"), x, y)!;

    internal static (float X, float Y, float Z) Read3(object vector) =>
        (Field(vector, "X"), Field(vector, "Y"), Field(vector, "Z"));

    internal static float Field(object value, string name) => (float)value.GetType().GetField(name)!.GetValue(value)!;

    internal static object Keyboard(params string[] keys)
    {
        var keysType = MonoGameType("Microsoft.Xna.Framework.Input.Keys");
        var array = Array.CreateInstance(keysType, keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            array.SetValue(Enum.Parse(keysType, keys[i]), i);
        }

        return Activator.CreateInstance(MonoGameType("Microsoft.Xna.Framework.Input.KeyboardState"), [array])!;
    }

    internal static bool IsDown(object padState, string button) =>
        (bool)padState.GetType().GetMethod("IsButtonDown")!.Invoke(padState, [Enum.Parse(MonoGameType("Microsoft.Xna.Framework.Input.Buttons"), button)])!;

    internal static object ConnectedPad(float stickX, float stickY, params string[] buttons)
    {
        var buttonsType = MonoGameType("Microsoft.Xna.Framework.Input.Buttons");
        var array = Array.CreateInstance(buttonsType, buttons.Length);
        for (var i = 0; i < buttons.Length; i++)
        {
            array.SetValue(Enum.Parse(buttonsType, buttons[i]), i);
        }

        var padType = MonoGameType("Microsoft.Xna.Framework.Input.GamePadState");
        return Activator.CreateInstance(padType, Vector2(stickX, stickY), Vector2(0f, 0f), 0f, 0f, array)!;
    }

    internal static object Invoke(string type, string method, params object[] arguments)
    {
        var target = CompatType(type);
        var info = target.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        return info.Invoke(null, arguments)!;
    }

    private static Assembly Build()
    {
        var path = MonoGameFactAttribute.Path()
            ?? throw new InvalidOperationException("MonoGame is not in the NuGet cache.");
        _monoGame = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        var problems = CompileProblems();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems));
        }

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(path)
            .Select(p => MetadataReference.CreateFromFile(p));
        var sources = new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, includeDevice: true)
            .Where(c => c.RelativePath.EndsWith(".cs", StringComparison.Ordinal))
            .Select(c => CSharpSyntaxTree.ParseText(c.NewText!, new CSharpParseOptions(LanguageVersion.CSharp12), c.RelativePath));
        var compilation = CSharpCompilation.Create(
            "XnaReborn.Compat.DeviceTests",
            sources,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        if (!emitted.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitted.Diagnostics));
        }

        return Assembly.Load(stream.ToArray());
    }
}
