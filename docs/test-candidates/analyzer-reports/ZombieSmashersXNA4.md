# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 31 / 100 |
| Projected score after automatic fixes | 41 / 100 |
| Projects analyzed | 3 |
| Findings | 57 |

Severity: Blocker 4, Error 36, Warning 11, Info 6

Classification: AutoFixable 24, Manual 9, Unsupported 24

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj | 4.0 | Windows | HiDef | v4.0 |
| GameZS/GameZS/GameZS/GameZS.csproj | 4.0 | Windows | HiDef | v4.0 |
| MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +6 more`
- **[Error] XNA003** XNA assembly references
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +6 more`
- **[Error] XNA003** XNA assembly references
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +6 more`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `..\CharacterEditorZSContent\CharacterEditorZSContent.contentproj`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `..\GameZSContent\GameZSContent.contentproj`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `..\MapEditorZSContent\MapEditorZSContent.contentproj`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `NoStdLib=true`
- **[Info] XNA002** XNA-specific project properties
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Code: `.NETFramework,Version=v4.0,Profile=Client, Microsoft.Net.Client.3.5, Microsoft.Net.Framework.3.5.SP1 +2 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `.NETFramework,Version=v4.0,Profile=Client, Microsoft.Net.Client.3.5, Microsoft.Net.Framework.3.5.SP1 +2 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Code: `.NETFramework,Version=v4.0,Profile=Client, Microsoft.Net.Client.3.5, Microsoft.Net.Framework.3.5.SP1 +2 more`

### Manual

- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Location: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/Game1.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Game1.cs:10:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/store/Store.cs:4:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Location: `MapEditorZS/MapEditorZS/MapEditorZS/Game1.cs:10:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Warning] XNA015** XACT audio engine (AudioEngine, WaveBank, SoundBank, Cue)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/audio/Sound.cs:11:24`
  - Code: `private static AudioEngine engine;`
- **[Warning] XNA015** XACT audio engine (AudioEngine, WaveBank, SoundBank, Cue)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/audio/Sound.cs:17:26`
  - Code: `engine = new AudioEngine(@"Content/sfx/sfxproj.xgs");`
- **[Warning] XNA015** XACT audio engine (AudioEngine, WaveBank, SoundBank, Cue)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/audio/Sound.cs:22:23`
  - Code: `public static AudioEngine GetEngine()`
- **[Warning] XNA016** XACT project file (.xap)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `sfx/sfxproj.xap`
- **[Warning] XNA017** Effect file (.fx) needs shader review
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Code: `fx/bloom.fx, fx/filter.fx, fx/negative.fx +2 more`

### Unsupported

- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Location: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/Game1.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Game1.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/store/Store.cs:5:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Location: `MapEditorZS/MapEditorZS/MapEditorZS/Game1.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/CharacterEditorZS.csproj`
  - Location: `CharacterEditorZS/CharacterEditorZS/CharacterEditorZS/Game1.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/CharClasses/Character.cs:14:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Game1.cs:9:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Blood.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/BloodDust.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Bullet.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Fire.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Fog.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Heat.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Hit.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/MuzzleFlash.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Particle.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/ParticleManager.cs:9:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Rocket.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Shockwave.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/Particles/Smoke.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/net/NetConnect.cs:4:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/net/NetGame.cs:5:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `GameZS/GameZS/GameZS/GameZS.csproj`
  - Location: `GameZS/GameZS/GameZS/net/NetPlay.cs:4:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `MapEditorZS/MapEditorZS/MapEditorZS/MapEditorZS.csproj`
  - Location: `MapEditorZS/MapEditorZS/MapEditorZS/Game1.cs:9:1`
  - Code: `using Microsoft.Xna.Framework.Net;`

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

### XNA009 GamerServices (Xbox Live, Guide, gamer profiles)

Code uses GamerServices (Guide, SignedInGamer, achievements, leaderboards, trial mode). MonoGame removed this namespace and no replacement exists for the Xbox Live services behind it.

**Resolution:** Remove the calls or replace them with your own sign-in, saving, and leaderboard code. Remove GamerServicesComponent from the Game constructor.

### XNA010 Net (NetworkSession, system link, Live multiplayer)

Code uses the XNA Net namespace (NetworkSession, PacketReader, PacketWriter). MonoGame removed it, and it depended on Xbox Live matchmaking.

**Resolution:** Rewrite networking with System.Net.Sockets or a library such as Lidgren.Network.

### XNA012 Storage (StorageDevice, StorageContainer)

Code uses StorageDevice and StorageContainer for save games. MonoGame removed the Storage namespace because it did not port across platforms.

**Resolution:** Write saves with System.IO under Environment.SpecialFolder.ApplicationData and read packaged files with TitleContainer.OpenStream. The optional shim project (project-migrator:12.1) can stand in for the calls while you rewrite.

### XNA015 XACT audio engine (AudioEngine, WaveBank, SoundBank, Cue)

Code uses the XACT runtime. MonoGame can load compiled XACT files (.xgs, .xwb, .xsb) but the content builder cannot compile XACT projects, and some features such as ADPCM and streaming wave banks are limited.

**Resolution:** Ship the compiled XACT files unchanged and load them by path, or move sounds to SoundEffect and Song assets and drop XACT.

### XNA016 XACT project file (.xap)

The project contains an XACT authoring file. XACT is a Windows-only tool that is no longer updated, and the MonoGame content builder does not compile .xap files.

**Resolution:** Compile the project in XACT and copy the .xgs, .xwb, and .xsb outputs, or export the sounds as wav or ogg files and use SoundEffect and Song.

### XNA017 Effect file (.fx) needs shader review

XNA compiled .fx files with the legacy DirectX 9 compiler. MonoGame uses a different compiler and imposes restrictions, so old shaders can fail to build or render differently.

**Resolution:** Build the effect with the MonoGame content builder, fix compile errors, and compare the output. The MonoGame custom effects guide lists the limits.

### XNA045 ClickOnce bootstrapper packages

The project lists ClickOnce bootstrapper packages, which come from the Publish tab of Visual Studio 2010 (the XNA templates include the XNA Framework redistributable). The same settings usually bring a hard-coded PublishUrl, ManifestKeyFile, and manifest signing. Nothing in an SDK-style MonoGame build uses them. Known limit: only BootstrapperPackage items trigger the rule, not the publish properties alone.

**Resolution:** The migrator writes a new SDK-style project that carries none of the publish settings. Publish the MonoGame game with dotnet publish.

Migrator task: project-migrator:4.1

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA047 NoStdLib breaks SDK-style builds

NoStdLib=true tells the compiler not to reference the standard library. The old XNA project templates set it. An SDK-style project supplies the references itself, so the compiler would miss System.Object and the build would fail.

**Resolution:** The migrator writes a new SDK-style project from a fixed property set that does not include NoStdLib.

Migrator task: project-migrator:4.1

### XNA049 Content project referenced with ProjectReference

The game project references a .contentproj with a ProjectReference (XnaReferenceType Content), the form used when the content project is a sibling project. MonoGame cannot load that reference and builds content from Content.mgcb instead. The analyzer reads this content project like a nested one, so the content-item rules (XNA017, XNA019, XNA020, XNA021) see the assets behind this form.

**Resolution:** The migrator removes the reference, generates Content.mgcb from the content project like it does for a nested content project (XNA005), copies the source assets into the game project, and lists anything it cannot convert as manual work.

Migrator task: project-migrator:7.1
