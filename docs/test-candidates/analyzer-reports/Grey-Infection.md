# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 37 / 100 |
| Projected score after automatic fixes | 47 / 100 |
| Projects analyzed | 1 |
| Findings | 71 |

Severity: Blocker 60, Error 6, Warning 3, Info 2

Classification: AutoFixable 8, Manual 2, Unsupported 61

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj | 4.0 | Xbox360 | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Avatar, Microsoft.Xna.Framework.Game +6 more`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `..\WhiteAndWorldContent\WhiteAndWorldContent.contentproj`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `NoStdLib=true`
- **[Info] XNA002** XNA-specific project properties
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `.NETFramework,Version=v4.0,Profile=Client, Microsoft.Net.Client.3.5, Microsoft.Net.Framework.3.5.SP1 +2 more`

### Manual

- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/StorageSession.cs:6:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Warning] XNA017** Effect file (.fx) needs shader review
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `Shaders/GreyScale.fx`

### Unsupported

- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/CrashDebugGame.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameBase.cs:9:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/EnemyAnimationEngine.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Background.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Bullet.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Bullet_Death.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Clue.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/EndGame/ConditionAlert.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/EndGame/WarningBox.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/EndTrial/CheckList.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/EndTrial/FeatureString.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/EndTrial/Thumbnail.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Enemy.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Enemy_Death.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/ExitArea.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Firearm.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/BackBox.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/Button.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/ButtonGroup.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/ConfirmationScreen.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/GridButtonGroup.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/HUD.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/InputHintLabel.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/InputInfoFooter.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/Label.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/LevelSelectionButton.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/LoadingMessage.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/MainWindowFrame.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/PauseMenu.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/SliderButton.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/TextButton.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/GUI/Window.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Intro/IntroBox.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/MainMenu/OptionMenuCell.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/MainMenu/PurchaseButton.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Options/GameInputMap.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Options/Slider.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Player.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/PlayerSignedOut.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/ResetLevel.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/ScanLines.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/StorageDeviceLost.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/Tile.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Entities/TileGroupQuery.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/Fog.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/GameString.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/GlitchColor.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/GreyInfectionTittleScene.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/JossStressAnimationEngine.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/Credits.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/EndTrial.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/Game.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/GameOver.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/GameTitle.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/Intro.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/LevelSelection.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/Menu.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/Options.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/States/SplashScreen.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Location: `WhiteAndWorld/GameObjects/StorageSession.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Error] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `WhiteAndWorld/WhiteAndWorld (Xbox 360).csproj`
  - Code: `XnaPlatform=Xbox 360`

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

XnaPlatform names a device platform. Xbox 360 and Zune builds depend on device APIs, and neither platform can be a MonoGame DesktopGL target. Code in this project is likely to use platform-only APIs.

**Resolution:** Migrate the Windows project of the solution instead. Keep shared code files and remove the device-specific project and its #if branches by hand.

### XNA009 GamerServices (Xbox Live, Guide, gamer profiles)

Code uses GamerServices (Guide, SignedInGamer, achievements, leaderboards, trial mode). MonoGame removed this namespace and no replacement exists for the Xbox Live services behind it.

**Resolution:** Remove the calls or replace them with your own sign-in, saving, and leaderboard code. Remove GamerServicesComponent from the Game constructor.

### XNA012 Storage (StorageDevice, StorageContainer)

Code uses StorageDevice and StorageContainer for save games. MonoGame removed the Storage namespace because it did not port across platforms.

**Resolution:** Write saves with System.IO under Environment.SpecialFolder.ApplicationData and read packaged files with TitleContainer.OpenStream. The optional shim project (project-migrator:12.1) can stand in for the calls while you rewrite.

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

## Diagnostics

- ANA003: Compile item points to a missing file: WhiteAndWorld/GameObjects/Entities/SplashScreen/Logos/UndeadCodeLogo.cs (`WhiteAndWorld/GameObjects/Entities/SplashScreen/Logos/UndeadCodeLogo.cs`)
