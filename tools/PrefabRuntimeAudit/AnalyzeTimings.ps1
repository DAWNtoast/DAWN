param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$report = Get-Content -LiteralPath (Join-Path $OutputDirectory 'memory.json') -Raw | ConvertFrom-Json
if ($report.status -ne 'passed' -or $report.actions.Count -lt 300) { throw 'Incomplete action sequence.' }
if (@($report.actions.id | Select-Object -Unique).Count -ne $report.actions.Count) { throw 'Duplicate action IDs.' }
$probeRevision = if ($report.probeRevision) { $report.probeRevision } else { 'v1' }
$verifiedSceneProbe = $probeRevision -eq 'v2'
$rows = @(foreach($action in $report.actions) {
    if ($action.status -eq 'pending' -or $action.callbackMs -lt 0) { throw ('Unclosed action: '+$action.id+' '+$action.action) }
    if ($action.firstRenderedMs -ge 0 -and ($action.firstRenderedMs -lt $action.callbackMs -or $action.firstRenderedFrame -lt $action.startFrame)) { throw ('Non-monotonic first-frame timing: '+$action.id) }
    if ($action.status -eq 'rendered-ready' -and ($action.readyRenderedMs -lt $action.firstRenderedMs -or $action.readyRenderedFrame -lt $action.firstRenderedFrame)) { throw ('Non-monotonic ready-frame timing: '+$action.id) }
    $category = switch -Regex ($action.stage) {
        '^MenuModalCycle:1$' { 'MenuModalFirst'; break }
        '^MenuModalCycle:' { 'MenuModalWarm'; break }
        '^PortModalCycle:1$' { 'PortModalFirst'; break }
        '^PortModalCycle:' { 'PortModalWarm'; break }
        '^FirstFacility:' { 'FacilityFirst'; break }
        '^FacilityCycle:' { 'FacilityWarm'; break }
        '^SceneCycle:' { 'SceneWarm'; break }
        default { $action.stage }
    }
    # Achievements is already opened once during Startup before modal cycle 1.
    # Use each open/close action's true occurrence rather than its loop's stage.
    if ($action.action -in @('Click:Achievements','Click:Back From Achievements')) {
        $category = if ([int]$action.occurrence -eq 1) { 'MenuModalFirst' } else { 'MenuModalWarm' }
    }
    $sceneRequest = $action.action.StartsWith('LoadScene:')
    $startup = $action.action -eq 'Startup:BeforeFirstScene'
    if ($verifiedSceneProbe -and ($sceneRequest -or $startup) -and
        (-not $action.targetSceneRenderVerified -or $action.targetSceneLoadedMs -lt 0 -or $action.firstRenderedMs -lt $action.targetSceneLoadedMs -or $action.firstRenderedFrame -lt $action.targetSceneLoadedFrame)) {
        throw ('Target scene first frame not verified: '+$action.id)
    }
    $renderAllowed = -not $sceneRequest -or $verifiedSceneProbe
    $meaning = if ($startup) { 'BootstrapBeforeSceneToSceneLoaded; includes initial scene Awake/OnEnable, not a pure UI callback' }
        elseif ($sceneRequest) { 'LoadSceneRequestReturnOnly; target scene may activate on a later frame' }
        else { 'SynchronousProductionCallbackReturn' }
    [pscustomobject]@{Id=$action.id;Stage=$action.stage;Category=$category;Action=$action.action;Occurrence=$action.occurrence;Status=$action.status;
        CallbackMs=$action.callbackMs;CallbackMeaning=$meaning;BindAndLayoutMs=if($action.bindAndLayoutMs-ge0){$action.bindAndLayoutMs}else{$null};
        TargetSceneLoadedMs=if($verifiedSceneProbe -and ($sceneRequest -or $startup)){$action.targetSceneLoadedMs}else{$null};
        FirstRenderedMs=if($renderAllowed -and $action.firstRenderedMs-ge0){$action.firstRenderedMs}else{$null};
        ReadyRenderedMs=if($renderAllowed -and $action.status-eq'rendered-ready'){$action.readyRenderedMs}else{$null};
        FrameDelta=if($renderAllowed -and $action.firstRenderedMs-ge0){$action.firstRenderedFrame-$action.startFrame}else{$null};
        RenderTimingAvailability=if(-not $renderAllowed){'Unavailable: v1 probe can close on old-scene frame before target activates'}elseif($action.firstRenderedMs-lt0){'Unavailable: superseded before any frame'}elseif($action.status-ne'rendered-ready'){'First frame only: superseded before ready'}else{'Available: controlled camera CPU end-render, not GPU/display present'}}
})
$rows | Export-Csv -LiteralPath (Join-Path $OutputDirectory 'timings.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{Result='PASS';AnalysisVersion=3;ProbeRevision=$probeRevision;RawReportSHA256=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'memory.json')).Hash;
    Actions=$rows.Count;RenderedReady=@($rows|Where-Object Status -eq 'rendered-ready').Count;Superseded=@($rows|Where-Object Status -like 'superseded*').Count;
    SceneRequestRenderMetricsExcluded=@($rows|Where-Object {$_.Action -like 'LoadScene:*' -and $null -eq $_.FirstRenderedMs}).Count;
    VisualCaptureRun=$report.visualCaptureRun;Method=$report.timingMethod;
    AnalysisNotes='Achievements open/back uses actual occurrence. Startup callback is bootstrap-to-sceneLoaded, first frame is bootstrap-to-initial-menu rendering; not process launch time. v1 LoadScene first/ready metrics excluded. v2 requires target sceneLoaded plus active scene handle/Canvas at camera end-render. LoadScene callback remains request submission only. Negative sentinel metrics unavailable. Only derived files changed; raw report authoritative.'} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'timing-summary.json') -Encoding utf8
