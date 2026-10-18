param([Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Smoke','Package','Seal')][string]$Stage)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '../NavalDragRouteRelease20261015/Release.ps1') -Stage $Stage `
    -ReleaseDirectory 'D:/Game/Builds/第七港口-海战范围显示优先级20261016' `
    -EvidenceDirectory 'D:/Game/Artifacts/NavalRangePriorityRelease20261016' `
    -Version '0.3.20261016.2' `
    -ZipFileName '第七港口-Windows-20261016-海战范围显示优先级.zip' `
    -Validation 'See Artifacts/NavalRangePriority20261016/verification-summary.json'
exit $LASTEXITCODE
