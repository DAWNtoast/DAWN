param([Parameter(Mandatory=$true)][string]$Project)
$ErrorActionPreference = 'Stop'
$memoryProject = [IO.Path]::GetFullPath($Project)
if (-not $memoryProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $memoryProject 'Assets/_Project/Scenes/MainMenu.unity'))) {
    throw 'Use an existing isolated Unity project under D:/Game/tmp.'
}
# Deliberately no Unity launch/build. Root must call this only while the project is idle.
$memoryRuntime = Join-Path $memoryProject 'Assets/RuntimeMemoryAudit'
$memoryEditor = Join-Path $memoryRuntime 'Editor'
if (Test-Path -LiteralPath $memoryRuntime) { throw 'Preserve the existing diagnostic folder; inspect it before another preparation.' }
New-Item -ItemType Directory -Path $memoryEditor -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RuntimeMemoryPlayerProbe.cs') -Destination $memoryRuntime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RuntimeMemoryAuditBuilder.cs') -Destination $memoryEditor
@'
{
  "name": "ShatteredSail.RuntimeMemoryAudit",
  "references": ["ShatteredSail.Presentation", "ShatteredSail.Core", "ShatteredSail.Content", "Unity.UGUI"],
  "autoReferenced": true
}
'@ | Set-Content -LiteralPath (Join-Path $memoryRuntime 'ShatteredSail.RuntimeMemoryAudit.asmdef') -Encoding utf8
@'
{
  "name": "ShatteredSail.RuntimeMemoryAudit.Editor",
  "references": [],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
'@ | Set-Content -LiteralPath (Join-Path $memoryEditor 'ShatteredSail.RuntimeMemoryAudit.Editor.asmdef') -Encoding utf8
Write-Output ('PREPARED=' + $memoryRuntime)
Write-Output 'BUILD_METHOD=ShatteredSail.Editor.RuntimeMemoryAuditBuilder.BuildFromCommandLine'
Write-Output 'BUILD_ARGUMENT=-runtimeMemoryBuildOutput <fresh audit .exe>'
Write-Output 'PLAYER_ARGUMENT=-runtimeMemoryPlayerOutput <fresh audit .json>'
Write-Output 'REMOVE_BEFORE_DELIVERY=Assets/RuntimeMemoryAudit plus its .meta; verify no diagnostic assembly in shipped Managed directory.'
