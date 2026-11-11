using System.Reflection;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Shims;

namespace XnaReborn.Tests.Device;

/// <summary>
/// Device-to-desktop tasks 7 and 8: the generated device part of XnaReborn.Compat compiles without warnings and its pure classes behave.
/// The sources are compiled against the real MonoGame assembly from the NuGet cache; the tests skip when it is not cached.
/// </summary>
[Trait("Category", "Migrator")]
public sealed class DeviceSourcesTests
{
    private const float _tolerance = 1e-4f;

    // ---------- generation (no MonoGame needed) ----------
    [Fact]
    public void The_device_files_are_added_only_when_asked_for()
    {
        var generator = new ShimProjectGenerator();

        Assert.Equal(4, generator.Generate(MonoGameTarget.DesktopGL).Count);
        var files = generator.Generate(MonoGameTarget.DesktopGL, includeDevice: true).Select(f => f.RelativePath).ToList();

        Assert.Equal(
            [
                "XnaReborn.Compat/XnaReborn.Compat.csproj",
                "XnaReborn.Compat/CompletedAsyncResult.cs",
                "XnaReborn.Compat/GamerServices.cs",
                "XnaReborn.Compat/Storage.cs",
                "XnaReborn.Compat/Device/DeviceControls.cs",
                "XnaReborn.Compat/Device/DeviceRuntime.cs",
                "XnaReborn.Compat/Device/Accelerometer.cs",
                "XnaReborn.Compat/Device/TiltMapper.cs",
                "XnaReborn.Compat/Device/PhoneShell.cs",
                "XnaReborn.Compat/Device/PhoneTasks.cs",
                "XnaReborn.Compat/Device/Marketplace.cs",
                "XnaReborn.Compat/Input/CompatGamePad.cs",
                "XnaReborn.Compat/Input/DeviceInput.cs",
                "XnaReborn.Compat/Input/GamePadMapper.cs",
            ],
            files);
    }

    [Fact]
    public void Device_types_use_the_namespaces_of_the_apis_they_stand_in_for()
    {
        var all = string.Join('\n', new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, includeDevice: true).Select(f => f.NewText));

        Assert.Contains("namespace Microsoft.Devices.Sensors", all, StringComparison.Ordinal);
        Assert.Contains("namespace Microsoft.Phone.Shell", all, StringComparison.Ordinal);
        Assert.Contains("namespace Microsoft.Phone.Tasks", all, StringComparison.Ordinal);
        Assert.Contains("namespace Microsoft.Phone.Marketplace", all, StringComparison.Ordinal);
        Assert.Contains("namespace XnaReborn.Compat.Input", all, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_finds_the_device_namespaces_and_types()
    {
        var uses = ShimProjectGenerator.Scan("using Microsoft.Devices.Sensors;\nusing Microsoft.Phone.Tasks;\nclass A { Accelerometer a; WebBrowserTask w; }");

        Assert.Equal(["Microsoft.Devices.Sensors", "Microsoft.Phone.Tasks"], uses.Select(u => u.Namespace));
        Assert.Equal(["Accelerometer"], uses[0].Types);
        Assert.Equal(["WebBrowserTask"], uses[1].Types);
    }

    [Fact]
    public void Generated_files_are_marked_as_shim_and_use_lf_line_endings()
    {
        Assert.All(new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, includeDevice: true), c =>
        {
            Assert.StartsWith("Shim:", c.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', c.NewText!);
        });
    }

    // ---------- compile (task 7.6) ----------
    [MonoGameFact]
    public void Generated_sources_with_the_device_part_compile_without_warnings()
    {
        Assert.Empty(DeviceCompat.CompileProblems());
    }

    [MonoGameFact]
    public void Namespaces_match_the_original_apis()
    {
        foreach (var name in new[]
        {
            "Microsoft.Devices.Sensors.Accelerometer",
            "Microsoft.Devices.Sensors.AccelerometerReadingEventArgs",
            "Microsoft.Devices.Sensors.SensorState",
            "Microsoft.Phone.Shell.PhoneApplicationService",
            "Microsoft.Phone.Tasks.WebBrowserTask",
            "Microsoft.Phone.Tasks.PhotoChooserTask",
            "Microsoft.Phone.Tasks.MarketplaceDetailTask",
            "Microsoft.Phone.Marketplace.LicenseInformation",
            "Microsoft.Xna.Framework.GamerServices.Guide",
            "XnaReborn.Compat.Input.CompatGamePad",
            "XnaReborn.Compat.Input.DeviceInput",
        })
        {
            Assert.NotNull(DeviceCompat.Assembly.GetType(name));
        }
    }

    // ---------- TiltMapper ----------
    private static object Input(bool left = false, bool right = false, bool up = false, bool down = false, object? mouse = null)
    {
        var vector2Nullable = typeof(Nullable<>).MakeGenericType(DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Vector2"));
        var offset = mouse is null ? Activator.CreateInstance(vector2Nullable) : Activator.CreateInstance(vector2Nullable, mouse);
        return Activator.CreateInstance(DeviceCompat.CompatType("XnaReborn.Compat.Device.TiltInput"), left, right, up, down, offset)!;
    }

    private static (float X, float Y, float Z) Target(object input) =>
        DeviceCompat.Read3(DeviceCompat.Invoke("XnaReborn.Compat.Device.TiltMapper", "Target", input));

    private static void AssertNear((float X, float Y, float Z) expected, (float X, float Y, float Z) actual)
    {
        Assert.InRange(actual.X, expected.X - _tolerance, expected.X + _tolerance);
        Assert.InRange(actual.Y, expected.Y - _tolerance, expected.Y + _tolerance);
        Assert.InRange(actual.Z, expected.Z - _tolerance, expected.Z + _tolerance);
    }

    [MonoGameFact]
    public void Tilt_with_no_key_is_the_resting_reading_face_up()
    {
        AssertNear((0f, 0f, -1f), Target(Input()));
    }

    [MonoGameFact]
    public void Tilt_with_each_key_gives_the_golden_reading()
    {
        AssertNear((0.7f, 0f, -0.714143f), Target(Input(right: true)));
        AssertNear((-0.7f, 0f, -0.714143f), Target(Input(left: true)));
        AssertNear((0f, 0.7f, -0.714143f), Target(Input(up: true)));
        AssertNear((0f, -0.7f, -0.714143f), Target(Input(down: true)));
    }

    [MonoGameFact]
    public void Tilt_with_two_keys_combines_and_opposite_keys_cancel()
    {
        AssertNear((0.7f, 0.7f, -0.141421f), Target(Input(right: true, up: true)));
        AssertNear((-0.7f, -0.7f, -0.141421f), Target(Input(left: true, down: true)));
        AssertNear((0f, 0f, -1f), Target(Input(left: true, right: true)));
    }

    [MonoGameFact]
    public void Tilt_reading_stays_close_to_one_g()
    {
        var (x, y, z) = Target(Input(right: true, up: true));

        Assert.InRange(Math.Sqrt((x * x) + (y * y) + (z * z)), 0.99, 1.01);
    }

    [MonoGameFact]
    public void Tilt_follows_the_mouse_offset_with_the_same_range_as_the_keys()
    {
        AssertNear((0f, 0f, -1f), Target(Input(mouse: DeviceCompat.Vector2(0f, 0f))));
        AssertNear((0.7f, 0f, -0.714143f), Target(Input(mouse: DeviceCompat.Vector2(1f, 0f))));
        AssertNear((0.7f, 0f, -0.714143f), Target(Input(mouse: DeviceCompat.Vector2(5f, 0f))));
        AssertNear((0f, 0.35f, -0.936750f), Target(Input(mouse: DeviceCompat.Vector2(0f, -0.5f))));
    }

    [MonoGameFact]
    public void Easing_covers_about_63_percent_after_one_time_constant_and_never_jumps()
    {
        var rest = DeviceCompat.Vector3(0f, 0f, -1f);
        var target = DeviceCompat.Vector3(0.7f, 0f, -0.714143f);

        var after = DeviceCompat.Read3(DeviceCompat.Invoke("XnaReborn.Compat.Device.TiltMapper", "Ease", rest, target, 0.12f));
        var none = DeviceCompat.Read3(DeviceCompat.Invoke("XnaReborn.Compat.Device.TiltMapper", "Ease", rest, target, 0f));
        var far = DeviceCompat.Read3(DeviceCompat.Invoke("XnaReborn.Compat.Device.TiltMapper", "Ease", rest, target, 10f));

        AssertNear((0.442485f, 0f, -1f + (0.285857f * 0.632121f)), after);
        AssertNear((0f, 0f, -1f), none);
        AssertNear((0.7f, 0f, -0.714143f), far);
    }

    // ---------- GamePadMapper ----------
    private static object Map(params string[] keys) =>
        DeviceCompat.Invoke("XnaReborn.Compat.Input.GamePadMapper", "FromKeyboard", DeviceCompat.Keyboard(keys));

    [MonoGameFact]
    public void Without_keys_the_pad_is_connected_and_nothing_is_pressed()
    {
        var pad = Map();

        Assert.True((bool)pad.GetType().GetProperty("IsConnected")!.GetValue(pad)!);
        Assert.All(Enum.GetNames(DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Input.Buttons")), name => Assert.False(DeviceCompat.IsDown(pad, name), name));
    }

    [MonoGameFact]
    public void Each_key_in_the_table_presses_its_button()
    {
        var table = new (string[] Keys, string Button)[]
        {
            (["Space"], "A"),
            (["Enter"], "A"),
            (["Z"], "B"),
            (["X"], "X"),
            (["C"], "Y"),
            (["Escape"], "Back"),
            (["Tab"], "Start"),
            (["Q"], "LeftShoulder"),
            (["E"], "RightShoulder"),
            (["Left"], "DPadLeft"),
            (["A"], "DPadLeft"),
            (["Right"], "DPadRight"),
            (["D"], "DPadRight"),
            (["Up"], "DPadUp"),
            (["W"], "DPadUp"),
            (["Down"], "DPadDown"),
            (["S"], "DPadDown"),
        };

        foreach (var (keys, button) in table)
        {
            Assert.True(DeviceCompat.IsDown(Map(keys), button), string.Join("+", keys) + " should press " + button);
        }
    }

    [MonoGameFact]
    public void Move_keys_set_the_left_stick_and_unmapped_keys_do_nothing()
    {
        var pad = Map("Right", "Up");
        var sticks = pad.GetType().GetProperty("ThumbSticks")!.GetValue(pad)!;
        var left = sticks.GetType().GetProperty("Left")!.GetValue(sticks)!;

        Assert.Equal(1f, DeviceCompat.Field(left, "X"));
        Assert.Equal(1f, DeviceCompat.Field(left, "Y"));
        Assert.False(DeviceCompat.IsDown(Map("F5"), "A"));
        Assert.False(DeviceCompat.IsDown(Map("Z"), "A"));
    }

    [MonoGameFact]
    public void A_connected_pad_passes_through_and_escape_adds_back()
    {
        var withA = DeviceCompat.ConnectedPad(0.5f, 0f, "A");

        var plain = DeviceCompat.Invoke("XnaReborn.Compat.Input.GamePadMapper", "Combine", withA, DeviceCompat.Keyboard());
        var escaped = DeviceCompat.Invoke("XnaReborn.Compat.Input.GamePadMapper", "Combine", withA, DeviceCompat.Keyboard("Escape"));
        var other = DeviceCompat.Invoke("XnaReborn.Compat.Input.GamePadMapper", "Combine", withA, DeviceCompat.Keyboard("Space"));

        Assert.True(DeviceCompat.IsDown(plain, "A"));
        Assert.False(DeviceCompat.IsDown(plain, "Back"));
        Assert.True(DeviceCompat.IsDown(escaped, "A"));
        Assert.True(DeviceCompat.IsDown(escaped, "Back"));
        Assert.False(DeviceCompat.IsDown(other, "B"));
        var sticks = escaped.GetType().GetProperty("ThumbSticks")!.GetValue(escaped)!;
        Assert.Equal(0.5f, DeviceCompat.Field(sticks.GetType().GetProperty("Left")!.GetValue(sticks)!, "X"), 3);
    }

    [MonoGameFact]
    public void A_disconnected_pad_is_replaced_by_the_keyboard()
    {
        var disconnected = Activator.CreateInstance(DeviceCompat.MonoGameType("Microsoft.Xna.Framework.Input.GamePadState"))!;

        var pad = DeviceCompat.Invoke("XnaReborn.Compat.Input.GamePadMapper", "Combine", disconnected, DeviceCompat.Keyboard("Space"));

        Assert.True((bool)pad.GetType().GetProperty("IsConnected")!.GetValue(pad)!);
        Assert.True(DeviceCompat.IsDown(pad, "A"));
    }

    [MonoGameFact]
    public void The_window_is_made_resizable_only_when_the_back_buffer_is_taller_than_the_display()
    {
        Assert.Equal(true, DeviceCompat.Invoke("XnaReborn.Compat.Input.WindowRules", "NeedsResize", 1200, 1080));
        Assert.Equal(false, DeviceCompat.Invoke("XnaReborn.Compat.Input.WindowRules", "NeedsResize", 800, 1080));
        Assert.Equal(false, DeviceCompat.Invoke("XnaReborn.Compat.Input.WindowRules", "NeedsResize", 1080, 1080));
    }

    // ---------- Accelerometer ----------
    [MonoGameFact]
    public void Accelerometer_start_fails_without_the_input_component_and_raises_readings_from_the_game_loop()
    {
        var runtime = DeviceCompat.CompatType("XnaReborn.Compat.Device.DeviceRuntime");
        var installed = runtime.GetProperty("InputInstalled", BindingFlags.Static | BindingFlags.NonPublic)!;
        var tilt = runtime.GetProperty("Tilt", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sensorType = DeviceCompat.CompatType("Microsoft.Devices.Sensors.Accelerometer");
        var tick = sensorType.GetMethod("TickAll", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sensor = Activator.CreateInstance(sensorType)!;
        var start = sensorType.GetMethod("Start")!;
        var stop = sensorType.GetMethod("Stop")!;
        try
        {
            installed.SetValue(null, false);
            var error = Assert.Throws<TargetInvocationException>(() => start.Invoke(sensor, null));
            Assert.Contains("XNA060", error.InnerException!.Message, StringComparison.Ordinal);
            Assert.Equal("Disabled", sensorType.GetProperty("State")!.GetValue(sensor)!.ToString());

            installed.SetValue(null, true);
            tilt.SetValue(null, DeviceCompat.Vector3(0.7f, 0f, -0.714143f));
            var readings = new List<(double X, double Z, int Thread)>();
            var handlerType = typeof(EventHandler<>).MakeGenericType(DeviceCompat.CompatType("Microsoft.Devices.Sensors.AccelerometerReadingEventArgs"));
            var handler = (Delegate)typeof(DeviceSourcesTests).GetMethod(nameof(Record), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(handlerType.GetGenericArguments()[0])
                .Invoke(null, [readings])!;
            sensorType.GetEvent("ReadingChanged")!.AddEventHandler(sensor, handler);

            start.Invoke(sensor, null);
            Assert.Equal("Ready", sensorType.GetProperty("State")!.GetValue(sensor)!.ToString());
            tick.Invoke(null, [TimeSpan.FromMilliseconds(10)]);
            Assert.Empty(readings);

            tick.Invoke(null, [TimeSpan.FromMilliseconds(15)]);
            var reading = Assert.Single(readings);
            Assert.Equal(0.7, reading.X, 3);
            Assert.Equal(-0.714143, reading.Z, 3);
            Assert.Equal(Environment.CurrentManagedThreadId, reading.Thread);

            stop.Invoke(sensor, null);
            tick.Invoke(null, [TimeSpan.FromMilliseconds(100)]);
            Assert.Single(readings);
        }
        finally
        {
            stop.Invoke(sensor, null);
            installed.SetValue(null, false);
            tilt.SetValue(null, DeviceCompat.Vector3(0f, 0f, -1f));
        }
    }

    private static Delegate Record<TArgs>(List<(double X, double Z, int Thread)> readings)
    {
        var x = typeof(TArgs).GetProperty("X")!;
        var z = typeof(TArgs).GetProperty("Z")!;
        EventHandler<TArgs> handler = (_, args) => readings.Add(((double)x.GetValue(args)!, (double)z.GetValue(args)!, Environment.CurrentManagedThreadId));
        return handler;
    }

    // ---------- phone shell, tasks, marketplace ----------
    [MonoGameFact]
    public void Phone_application_state_keeps_values_in_memory_and_events_can_be_hooked()
    {
        var type = DeviceCompat.CompatType("Microsoft.Phone.Shell.PhoneApplicationService");
        var current = type.GetProperty("Current")!.GetValue(null)!;
        var state = (System.Collections.IDictionary)type.GetProperty("State")!.GetValue(current)!;

        state["score"] = 42;

        Assert.Equal(42, (int)state["score"]!);
        Assert.Same(current, type.GetProperty("Current")!.GetValue(null));
        foreach (var name in new[] { "Launching", "Activated", "Deactivated", "Closing" })
        {
            var ev = type.GetEvent(name)!;
            var handler = Delegate.CreateDelegate(ev.EventHandlerType!, typeof(DeviceSourcesTests).GetMethod(nameof(Ignore), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(ev.EventHandlerType!.GetGenericArguments()[0]));
            ev.AddEventHandler(current, handler);
            ev.RemoveEventHandler(current, handler);
        }
    }

    private static void Ignore<TArgs>(object? sender, TArgs args)
    {
    }

    [MonoGameFact]
    public void Launchers_and_marketplace_tasks_return_without_doing_anything()
    {
        foreach (var name in new[] { "WebBrowserTask", "EmailComposeTask", "PhoneCallTask", "SmsComposeTask", "MarketplaceHubTask", "MarketplaceDetailTask", "MarketplaceReviewTask", "MarketplaceSearchTask" })
        {
            var task = Activator.CreateInstance(DeviceCompat.CompatType("Microsoft.Phone.Tasks." + name))!;
            task.GetType().GetMethod("Show")!.Invoke(task, null);
        }
    }

    [MonoGameFact]
    public void Choosers_fail_fast_and_name_the_rule()
    {
        foreach (var name in new[] { "PhotoChooserTask", "CameraCaptureTask", "SaveRingtoneTask" })
        {
            var task = Activator.CreateInstance(DeviceCompat.CompatType("Microsoft.Phone.Tasks." + name))!;
            var error = Assert.Throws<TargetInvocationException>(() => task.GetType().GetMethod("Show")!.Invoke(task, null));

            Assert.IsType<NotSupportedException>(error.InnerException);
            Assert.Contains("XNA059", error.InnerException!.Message, StringComparison.Ordinal);
            Assert.Contains(name, error.InnerException.Message, StringComparison.Ordinal);
        }
    }

    [MonoGameFact]
    public void The_game_is_always_the_full_version()
    {
        var license = Activator.CreateInstance(DeviceCompat.CompatType("Microsoft.Phone.Marketplace.LicenseInformation"))!;
        var guide = DeviceCompat.CompatType("Microsoft.Xna.Framework.GamerServices.Guide");

        Assert.Equal(false, license.GetType().GetMethod("IsTrial")!.Invoke(license, null));
        Assert.Equal(false, guide.GetProperty("IsTrialMode")!.GetValue(null));
        Assert.NotNull(guide.GetMethod("ShowMarketplace"));
    }
}
