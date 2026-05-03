#!/usr/bin/env powershell
# Gen metadata-only ref assemblies of Klei/Unity DLLs the build references.
# Commit the result (`lib/`) for CI.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

# GameFolder resolution mirrors Directory.Build.props's chain:
#   $env:GAMEFOLDER → Directory.Build.props.user → registry → default.
# For the .user case, Test-Path picks whichever GameFolder actually exists
# (so OS-conditional PropertyGroups sort themselves out without us having
# to evaluate MSBuild's Condition= attribute from PowerShell).
$managed = $env:GAMEFOLDER
if (-not $managed -and (Test-Path 'Directory.Build.props.user')) {
   $managed = ([xml](Get-Content 'Directory.Build.props.user' -Raw)).SelectNodes('//GameFolder') |
      ForEach-Object { $_.InnerText.Trim() } | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $managed) {
   $reg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 457140' -ErrorAction SilentlyContinue
   $root = if ($reg.InstallLocation) { $reg.InstallLocation } else { 'C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded' }
   $managed = Join-Path $root 'OxygenNotIncluded_Data\Managed'
}
if (-not (Test-Path $managed)) { throw "ONI Managed folder not found: $managed" }

New-Item -ItemType Directory -Force -Path lib | Out-Null

# Mirror the <Reference> list in Directory.Build.props.
$dlls = @(
   'Assembly-CSharp', 'Assembly-CSharp-firstpass'
   'UnityEngine', 'UnityEngine.CoreModule', 'UnityEngine.UI', 'UnityEngine.UIModule'
   'UnityEngine.IMGUIModule', 'UnityEngine.TextRenderingModule', 'UnityEngine.InputLegacyModule'
   'Unity.TextMeshPro', 'Newtonsoft.Json'
) | ForEach-Object { Join-Path $managed "$_.dll" }

# --all keeps non-public metadata (Krafs.Publicizer needs it); -c omits IL.
& refasmer -v --all -c -O lib @dlls
if ($LASTEXITCODE -ne 0) { throw 'refasmer failed' }
