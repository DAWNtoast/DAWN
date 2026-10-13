param([Parameter(Mandatory=$true)][string]$Report,[switch]$Apply)
$ErrorActionPreference='Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$taskSource=[IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskScratch=[IO.Path]::GetFullPath('D:\Game\tmp\warehouse-redraw-20261012')
$taskReportPath=[IO.Path]::GetFullPath($Report)
if (-not $taskReportPath.StartsWith('D:\Game\Artifacts\WarehouseRedraw20261012\validation\')) { throw 'Report must be inside warehouse validation output.' }
$taskReport=Get-Content -LiteralPath $taskReportPath -Raw | ConvertFrom-Json
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
$taskCopies=@()
foreach($taskScene in $taskReport.scenes) {
    if (-not $taskScene.path.StartsWith('Assets/_Project/Scenes/UIPreviewsForMe/') -or -not $taskScene.path.EndsWith('.unity')) { throw 'Unexpected scene path' }
    $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskSource $taskScene.path))
    $taskOrigin=[IO.Path]::GetFullPath((Join-Path $taskScratch $taskScene.path))
    if (-not $taskDestination.StartsWith($taskSource+'\') -or -not $taskOrigin.StartsWith($taskScratch+'\')) { throw 'Path escaped workspace' }
    if ((Hash $taskDestination) -ne $taskScene.beforeSha256) { throw "Concurrent production scene edit: $($taskScene.path)" }
    if ((Hash ($taskDestination+'.meta')) -ne $taskScene.metaSha256) { throw "Scene meta changed: $($taskScene.path)" }
    if ((Hash $taskOrigin) -ne $taskScene.afterSha256) { throw "Scratch scene changed after report: $($taskScene.path)" }
    $taskCopies+=@{Source=$taskOrigin;Destination=$taskDestination;Hash=$taskScene.afterSha256}
}
foreach($taskDependency in $taskReport.dependencies) {
    if (-not $taskDependency.StartsWith('Assets/_Project/Presentation/EditablePreviewAssets/')) { throw 'Unexpected dependency path' }
    foreach($taskSuffix in @('','.meta')) {
        $taskOrigin=[IO.Path]::GetFullPath((Join-Path $taskScratch ($taskDependency+$taskSuffix)))
        $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskSource ($taskDependency+$taskSuffix)))
        if (-not $taskDestination.StartsWith($taskSource+'\') -or -not $taskOrigin.StartsWith($taskScratch+'\')) { throw 'Dependency path escaped workspace' }
        if (Test-Path -LiteralPath $taskDestination) {
            if ((Hash $taskOrigin) -ne (Hash $taskDestination)) { throw "Existing authored dependency differs: $taskDependency$taskSuffix" }
        } else { $taskCopies+=@{Source=$taskOrigin;Destination=$taskDestination;Hash=(Hash $taskOrigin)} }
    }
}
if($Apply) {
    foreach($taskCopy in $taskCopies) {
        Copy-Item -LiteralPath $taskCopy.Source -Destination $taskCopy.Destination -Force
        if ((Hash $taskCopy.Destination) -ne $taskCopy.Hash) { throw 'Copyback verification failed' }
    }
}
Write-Output "$($taskReport.scenes.Count) scenes verified; $($taskCopies.Count) scene/dependency files $(if($Apply){'copied'}else{'ready'})."
