// The same aliases the migrator writes into game code: they win over the MonoGame types that `using Microsoft.Xna.Framework.Audio;` imports.
global using AudioCategory = XnaReborn.Compat.Xact.AudioCategory;
global using AudioEngine = XnaReborn.Compat.Xact.AudioEngine;
global using Cue = XnaReborn.Compat.Xact.Cue;
global using SoundBank = XnaReborn.Compat.Xact.SoundBank;
global using WaveBank = XnaReborn.Compat.Xact.WaveBank;
