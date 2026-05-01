# Oxygen Not Included: "Scope" mod

A Satisfactory/VScode/Discord-style 'command palette' / quicksearch UI for Oxygen Not Included.

<img width="540" height="540" alt="scope-beta-compressed" src="https://github.com/user-attachments/assets/1f0f1798-9cec-4007-8281-f8bed5c74825" />

## Build

Prereqs: .NET SDK 6+ (the project targets `netstandard2.1`), Oxygen Not Included installed.

```sh
dotnet build
```

The Debug build copies `Scope.dll` plus `mod_info.yaml` and `mod.yaml` into your ONI dev-mods folder (default: `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\dev\Scope`). Launch ONI → **Mods → Local Mods** → enable **DEV: Scope**.

## Steam install path

The build resolves your ONI install via the Windows registry by default. If that lookup misses (Steam library on a non-default drive, WSL, macOS, etc.), create `Directory.Build.props.user` (gitignored) with overrides:

```xml
<Project>
  <PropertyGroup>
    <GameFolder>D:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed</GameFolder>
    <ModFolder>C:\Users\you\Documents\Klei\OxygenNotIncluded\mods\dev</ModFolder>
  </PropertyGroup>
</Project>
```
