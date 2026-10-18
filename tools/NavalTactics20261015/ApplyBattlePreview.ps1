param([string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011')
$ErrorActionPreference = 'Stop'
$navalWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$navalProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$navalTmp = Join-Path $navalWorkspace 'tmp'
if (-not $navalProject.StartsWith($navalTmp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Only an isolated workspace/tmp source is accepted.' }
$navalRelative = 'Assets/_Project/Scenes/UIPreviews/UI_Battle.unity'
$navalSource = Join-Path $navalProject $navalRelative
$navalTarget = Join-Path (Join-Path $navalWorkspace 'UnityClient') $navalRelative
$navalText = [IO.File]::ReadAllText($navalSource)
if ($navalText -notmatch 'm_Name: Naval Tactical Battle Preview') { throw 'Expected a dedicated naval tactics preview produced by SaveBattlePreviewFromCommandLine.' }
if ($navalText -match 'UIPreviewsForMe') { throw 'Retired preview references are forbidden.' }
Copy-Item -LiteralPath $navalSource -Destination $navalTarget -Force
Write-Output ("Applied only the maintained battle scene: {0}" -f $navalTarget)
