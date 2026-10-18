param([Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Smoke','Package','Seal')][string]$Stage)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '../NavalDragRouteRelease20261015/Release.ps1') -Stage $Stage `
    -ReleaseDirectory 'D:/Game/Builds/第七港口-终点接舷与撞击20261016' `
    -EvidenceDirectory 'D:/Game/Artifacts/NavalArrivalContactRelease20261016' `
    -Version '0.3.20261016.1' `
    -ZipFileName '第七港口-Windows-20261016-终点接舷与撞击.zip' `
    -Validation 'See Artifacts/NavalArrivalContact20261016/verification-summary.json'
exit $LASTEXITCODE
