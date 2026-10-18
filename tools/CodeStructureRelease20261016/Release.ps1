param(
    [Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Smoke','Package','Seal')][string]$Stage,
    [ValidatePattern('^Release[0-9]*$')][string]$SnapshotName = 'Release04'
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '../ReleaseBuild/DesktopRelease.ps1') -Stage $Stage `
    -ReleaseDirectory 'D:/Game/Builds/第七港口-代码结构优化20261016' `
    -EvidenceDirectory (Join-Path 'D:/Game/Artifacts/CodeStructureRelease20261016' $SnapshotName) `
    -Version '0.3.20261016.3' `
    -ZipFileName '第七港口-Windows-20261016-代码结构优化.zip' `
    -ValidationSummary 'D:/Game/Artifacts/CodeStructureRelease20261016/verification-summary.json'
exit $LASTEXITCODE
