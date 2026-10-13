param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
function Audit-Utc($Value) {
    if ($Value -is [DateTime]) { return [DateTimeOffset]::new($Value).ToUniversalTime() }
    if ($Value -is [DateTimeOffset]) { return $Value.ToUniversalTime() }
    return [DateTimeOffset]::Parse([string]$Value).ToUniversalTime()
}
function Audit-Median($Values) {
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    $mid = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return [double]$sorted[$mid] }
    return ([double]$sorted[$mid-1] + [double]$sorted[$mid]) / 2
}
$analysisRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $analysisRoot.StartsWith('D:\Game\Artifacts\RuntimeMemoryOptimization20261014\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected analysis path.' }
$report = Get-Content -Raw -LiteralPath (Join-Path $analysisRoot 'memory.json') | ConvertFrom-Json
$process = Get-Content -Raw -LiteralPath (Join-Path $analysisRoot 'process-summary.json') | ConvertFrom-Json
$samples = @(Import-Csv -LiteralPath (Join-Path $analysisRoot 'process-memory.csv') | ForEach-Object { [pscustomobject]@{Utc=Audit-Utc $_.Utc;PrivateBytes=[double]$_.PrivateBytes;WorkingSetBytes=[double]$_.WorkingSetBytes} })
if ($report.status -ne 'passed' -or $report.checkpoints.Count -ne 57 -or $report.expectedCheckpoints -ne 57 -or
    $report.facilityCycles -ne 20 -or $report.crewDetails -ne 36 -or $report.navigationVariants -ne 9 -or $report.ramVariants -ne 4 -or $report.sceneCycles -ne 3 -or
    $report.developmentBuild -or -not $report.identityVerified -or -not $report.controlledOffscreenRendering -or $report.renderedCameraFrames -lt 100 -or
    $process.ExitCode -ne 0 -or $process.TimedOut -or $samples.Count -lt 100) { throw 'Incomplete/nonrendered diagnostic cannot pass.' }
if (@($report.checkpoints.name | Select-Object -Unique).Count -ne 57) { throw 'Duplicate checkpoint names.' }
$stepStart = Audit-Utc $report.startedUtc
$navigationEvidence = [Collections.Generic.List[object]]::new()
$navigationNames = @('navigation_header_v3','navigation_compass_v3','navigation_panel_frame_v3')
$rows = @(foreach ($point in $report.checkpoints) {
    $begin = Audit-Utc $point.windowStartUtc
    $end = Audit-Utc $point.sampleUtc
    $window = @($samples | Where-Object { $_.Utc -ge $begin -and $_.Utc -lt $end })
    $near = $samples | Where-Object { $_.Utc -le $end } | Select-Object -Last 1
    if ($null -eq $near -or ($end-$near.Utc).TotalSeconds -gt .7) { throw ('No external sample near ' + $point.name) }
    if (-not $point.transient -and ($window.Count -lt 4 -or $point.renderedFramesInWindow -lt 30)) { throw ('Insufficient steady samples/rendered frames: ' + $point.name) }
    if ($point.transient -and $point.renderedFramesInWindow -lt 1) { throw ('Unrendered comic: ' + $point.name) }
    if ($point.activeRootCanvases.Count -eq 0) { throw ('No production Canvas: ' + $point.name) }
    $step = @($samples | Where-Object { $_.Utc -ge $stepStart -and $_.Utc -lt $end })
    if ($step.Count -eq 0) { $step = @($near) }
    $details = Get-Content -Raw -LiteralPath $point.detailsFile | ConvertFrom-Json
    $navigationTextures = @($details.textures | Where-Object { $_.name -in $navigationNames } | Sort-Object name,instanceId)
    $navigationEvidence.Add([pscustomobject]@{Checkpoint=$point.name;InstanceCount=$navigationTextures.Count;Textures=@($navigationTextures|Select-Object name,instanceId,width,height,format,mipmaps,readable)})
    if (@($details.renderTextures | Where-Object { $_.name -eq 'Common Memory Audit SDR Output' -and $_.width -eq 1920 -and $_.height -eq 1080 -and $_.created }).Count -ne 1) { throw 'Missing common offscreen output.' }
    [pscustomobject]@{
        Checkpoint=$point.name;Phase=$point.phase;Port=$point.port;Transient=$point.transient;StableSamples=$window.Count;RenderedFrames=$point.renderedFramesInWindow;
        StablePrivateMiB=if($point.transient){$null}else{(Audit-Median $window.PrivateBytes)/1MB};StableWorkingSetMiB=if($point.transient){$null}else{(Audit-Median $window.WorkingSetBytes)/1MB};
        CheckpointPrivateMiB=$near.PrivateBytes/1MB;CheckpointWorkingSetMiB=$near.WorkingSetBytes/1MB;AlignmentErrorSeconds=($end-$near.Utc).TotalSeconds;
        StepPeakPrivateMiB=($step|Measure-Object PrivateBytes -Maximum).Maximum/1MB;StepPeakWorkingSetMiB=($step|Measure-Object WorkingSetBytes -Maximum).Maximum/1MB;
        ManagedUsedMiB=$point.managedUsedBytes/1MB;ManagedReservedMiB=$point.managedReservedBytes/1MB;UnityAllocatedMiB=$point.allocatedBytes/1MB;UnityReservedMiB=$point.reservedBytes/1MB;
        GameObjects=$point.gameObjects;Graphics=$point.graphics;Textures=$point.textures;Sprites=$point.sprites;Materials=$point.materials;AudioClips=$point.audioClips;Fonts=$point.loadedFonts;
        ProductionCollections=$point.productionCollections;TextureByteUnavailableCount=$point.textureSizeUnavailable;WindowStartUtc=$begin.ToString('o');SampleUtc=$end.ToString('o')
        TargetNavigationTextureInstances=$navigationTextures.Count;TargetNavigationTextureNames=($navigationTextures.name -join ';')
    }
    $stepStart = Audit-Utc $point.inventoryCompletedUtc
})
$rows | Export-Csv -LiteralPath (Join-Path $analysisRoot 'checkpoints.csv') -NoTypeInformation -Encoding utf8
$navigationEvidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $analysisRoot 'navigation-texture-checkpoints.json') -Encoding utf8
$menus = @($rows | Where-Object Checkpoint -like '15_SceneCycle_Menu_*')
$facilities = @($rows | Where-Object Checkpoint -like '06_FacilityCycle_*')
$countStability = @(foreach ($group in @(@{Name='Menu';Items=$menus},@{Name='Facilities';Items=$facilities})) {
    foreach ($field in @('GameObjects','Graphics','Textures','Sprites','Materials','AudioClips','Fonts')) {
        $values = @($group.Items | Select-Object -ExpandProperty $field)
        [pscustomobject]@{Group=$group.Name;Field=$field;Values=$values;Stable=(@($values|Select-Object -Unique).Count -eq 1)}
    }
})
$summary = [pscustomobject]@{
    Result='PASS';Checkpoints=57;RenderedFrames=$report.renderedCameraFrames;ControlledOffscreen=$true;Unity=$report.unity;GPU=$report.gpu;GraphicsApi=$report.graphicsApi;
    GameplayStepPeakPrivateMiB=($rows|Measure-Object StepPeakPrivateMiB -Maximum).Maximum;GameplayStepPeakWorkingSetMiB=($rows|Measure-Object StepPeakWorkingSetMiB -Maximum).Maximum;
    AllProcessPeakPrivateMiB=$process.PeakPrivateMiB;AllProcessPeakWorkingSetMiB=$process.PeakWorkingSetMiB;
    MenuPrivateMiB=@($menus.StablePrivateMiB);MenuWorkingSetMiB=@($menus.StableWorkingSetMiB);MenuManagedMiB=@($menus.ManagedUsedMiB);
    MenuNavigationTextureInstances=@($menus.TargetNavigationTextureInstances);LastMenuNavigationTextureInstances=$menus[-1].TargetNavigationTextureInstances;LastMenuNavigationTextureNames=$menus[-1].TargetNavigationTextureNames;
    MenuPrivateDeltaMiB=$menus[-1].StablePrivateMiB-$menus[0].StablePrivateMiB;MenuWorkingSetDeltaMiB=$menus[-1].StableWorkingSetMiB-$menus[0].StableWorkingSetMiB;
    FacilityPrivateDeltaMiB=$facilities[-1].StablePrivateMiB-$facilities[0].StablePrivateMiB;ObjectCountStability=$countStability;
    Method=$report.method;Scope='PASS confirms sequence, isolation, actual rendering and measurement integrity. Evaluate deltas and assets separately; not a claim of zero bugs or unlimited-duration leak freedom. Step peaks exclude previous inventory intervals; AllProcess includes inventory/serialization overhead. Private commit and resident working set must not be added.'
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $analysisRoot 'summary.json') -Encoding utf8
$summary | Select-Object Result,Checkpoints,RenderedFrames,GameplayStepPeakPrivateMiB,GameplayStepPeakWorkingSetMiB,MenuPrivateDeltaMiB,MenuWorkingSetDeltaMiB | ConvertTo-Json
