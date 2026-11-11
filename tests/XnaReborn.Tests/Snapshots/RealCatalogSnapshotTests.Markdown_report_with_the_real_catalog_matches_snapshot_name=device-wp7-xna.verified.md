# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 60 / 100 |
| Projected score after automatic fixes | 70 / 100 |
| Projects analyzed | 1 |
| Findings | 18 |

Severity: Blocker 1, Error 3, Warning 4, Info 10

Classification: AutoFixable 17, Manual 0, Unsupported 1

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Wp7Xna.csproj | 4.0 | WindowsPhone | Reach | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Wp7Xna.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Wp7Xna.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +2 more`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Wp7Xna.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Wp7Xna.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `Wp7Xna.csproj`
  - Code: `XnaPlatform=Windows Phone`
- **[Warning] XNA053** Device conditional compilation symbols
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:31:5`
  - Code: `#if WINDOWS_PHONE`
- **[Warning] XNA054** Marketplace trial mode
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:101:34`
  - Code: `GraphicsDevice.Clear(Guide.IsTrialMode ? Color.DarkGray : Color.CornflowerBlue);`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Wp7Xna.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA057** Phone application service (tombstoning)
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:53:17`
  - Code: `if (PhoneApplicationService.Current.State.ContainsKey("score"))`
- **[Info] XNA057** Phone application service (tombstoning)
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:55:30`
  - Code: `score = (int)PhoneApplicationService.Current.State["score"];`
- **[Info] XNA057** Phone application service (tombstoning)
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:70:13`
  - Code: `PhoneApplicationService.Current.State["score"] = score;`
- **[Info] XNA060** Accelerometer
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:21:17`
  - Code: `private Accelerometer accelerometer;`
- **[Info] XNA060** Accelerometer
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:42:33`
  - Code: `accelerometer = new Accelerometer();`
- **[Info] XNA061** TouchPanel and gestures
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:37:13`
  - Code: `TouchPanel.EnabledGestures = GestureType.Tap | GestureType.FreeDrag;`
- **[Info] XNA061** TouchPanel and gestures
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:81:20`
  - Code: `while (TouchPanel.IsGestureAvailable)`
- **[Info] XNA061** TouchPanel and gestures
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:83:41`
  - Code: `GestureSample gesture = TouchPanel.ReadGesture();`
- **[Info] XNA062** GamePad.GetState (keyboard fallback and Back button)
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:76:17`
  - Code: `if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)`

### Unsupported

- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:5:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`

## Rules

### XNA001 XNA Game Studio build targets import

The project file imports the XNA Game Studio MSBuild targets. They exist only on machines with XNA Game Studio 4.0 and Visual Studio 2010 installed. Without them the build stops at once with MSB4019 (imported project not found), as it did for Pirate-Treasure-Hunter.

**Resolution:** The migrator writes an SDK-style project for .NET 8 that does not import the XNA targets.

Migrator task: project-migrator:4.1

### XNA002 XNA-specific project properties

The project sets XnaFrameworkVersion, a property that only the XNA Game Studio targets understand. The same project usually also sets ProjectTypeGuids (the XNA game GUID), XnaPlatform, XnaProfile, XnaOutputType, and XnaCrossPlatformGroupID. None has an MSBuild equivalent in MonoGame.

**Resolution:** The migrator drops XNA-specific properties and project type GUIDs when it generates the SDK-style project. The graphics profile is carried into Content.mgcb as /profile.

Migrator task: project-migrator:4.1

### XNA003 XNA assembly references

The project references XNA assemblies (Microsoft.Xna.Framework and related) by strong name with Private=False. They do not resolve on a machine without XNA, so the project cannot build. MonoGame ships the same API under the same namespaces in its own package. This rule also fires for assemblies MonoGame does not provide (GamerServices, Net, Avatar, Storage, Xact, Video); those have their own rules for code that uses them.

**Resolution:** The migrator replaces the XNA references with the MonoGame DesktopGL or WindowsDX package.

Migrator task: project-migrator:4.1

### XNA004 Old .NET Framework target

The project targets the .NET Framework (3.5 for XNA 3.x, 4.0 with the Client profile for XNA 4.0). That framework version is not available to an SDK-style build, and current MonoGame releases need modern .NET.

**Resolution:** The migrator sets the target framework of the generated project to net8.0.

Migrator task: project-migrator:4.1

### XNA007 Project targets Xbox 360, Zune, or Windows Phone

XnaPlatform names a device platform (Xbox 360, Zune, or Windows Phone). The project cannot run on a desktop as it is, because it depends on device APIs and device-only #if branches. The device rules XNA053 to XNA063 list what the code uses.

**Resolution:** The migrator turns the project into a Windows DesktopGL game: it skips platform copies of the same game, folds the device #if branches, and adds stand-ins that map touch, tilt, and the Back button to the mouse and keyboard. What it cannot convert is listed as manual work.

Migrator task: device-to-desktop:5.1

### XNA009 GamerServices (Xbox Live, Guide, gamer profiles)

Code uses GamerServices (Guide, SignedInGamer, achievements, leaderboards, trial mode). MonoGame removed this namespace and no replacement exists for the Xbox Live services behind it.

**Resolution:** Remove the calls or replace them with your own sign-in, saving, and leaderboard code. Remove GamerServicesComponent from the Game constructor.

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA053 Device conditional compilation symbols

An #if or #elif directive names a platform symbol (XBOX, XBOX360, ZUNE, WINDOWS_PHONE, or WINDOWS). The migrated project defines none of them, so the wrong branch would be compiled. Inactive branches count too.

**Resolution:** The migrator keeps the branch that is true for the chosen platform and removes the other branches. A condition that also depends on another symbol, such as DEBUG, is left as it is with a marker.

Migrator task: device-to-desktop:6.1

### XNA054 Marketplace trial mode

Code reads Guide.IsTrialMode. Trial mode and the marketplace belong to the Xbox and Phone stores, which no longer exist.

**Resolution:** The migrator adds a stand-in Guide member that always reports a full version.

Migrator task: device-to-desktop:7.3

### XNA057 Phone application service (tombstoning)

Code uses PhoneApplicationService to keep state when the phone deactivates the game.

**Resolution:** The migrator adds a stand-in with an in-memory State dictionary. Its events never fire.

Migrator task: device-to-desktop:7.2

### XNA060 Accelerometer

Code reads the phone accelerometer. A desktop has none, and MonoGame's Accelerometer is a stub in another namespace.

**Resolution:** The migrator adds an Accelerometer with the same members that is driven by the keyboard (arrow keys or W, A, S, D).

Migrator task: device-to-desktop:7.1

### XNA061 TouchPanel and gestures

Code reads touch input. MonoGame has TouchPanel, but on a desktop it only reports the mouse after two switches are turned on.

**Resolution:** The migrator adds a game component that turns the mouse touch point and mouse gestures on at startup.

Migrator task: device-to-desktop:8.2

### XNA062 GamePad.GetState (keyboard fallback and Back button)

Code reads the game pad. A phone or Zune has no pad, only a Back button, and many desktops have no pad either. The check is by name only: a game class called GamePad in another namespace is not told apart from the XNA one.

**Resolution:** The migrator changes the call to a replacement that returns the real pad when one is connected, and keyboard input otherwise (Escape is Back).

Migrator task: device-to-desktop:8.1
