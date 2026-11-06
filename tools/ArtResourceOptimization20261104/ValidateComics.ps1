param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Sync','Test','Status','Cleanup')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run = 'run1',
    [ValidateSet('EditMode','PlayMode')][string]$TestPlatform = 'EditMode',
    [switch]$Capture4K,
    [ValidatePattern('^[a-zA-Z0-9_.;]+$')][string]$TestFilter = 'ComicPanelResourceTests;OpeningPortComicPlaybackTests;OpeningPortComicProgressTests;ArrivalComicCatalogTests;LegendaryFleetComicPlaybackTests;LegendaryFleetComicReadProgressTests;LegendaryFleetComicCatalogTests;ComicPanelCatalogTests'
)
# PlayMode follow-up uses -TestFilter 'ComicAutoAdvanceTests;MainMenuSceneTests.NewGame_OpeningComicAutoAndClickFinishOnceAndRestorePortHubGuide;CoreGameplayDemoSceneTests.RamComic_;CoreGameplayDemoSceneTests.EveryTreasureRank_EntersDirectlyIntoNavigationWithoutAComic;CoreGameplayDemoSceneTests.TreasureCore_ContinueExploringLetsNearbyWaterEscapeAndMarkerRevisit'.
$ErrorActionPreference = 'Stop'
$main = 'D:\Game\UnityClient'
$scratch = 'D:\Game\tmp\component-rarity-halo-20261019'
$output = 'D:\Game\outputs\comic-pagination-20261103\art-opt-01-20261104'
$journal = Join-Path $output 'scratch-journal.json'
$lease = Join-Path $scratch 'ArtResourceOptimization20261104-Comics.lock'
$unity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$changes = [Collections.Generic.List[object]]::new()
$directories = [Collections.Generic.List[string]]::new()

function Assert-Safe([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith($scratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw ('Outside borrowed project: ' + $resolved) }
    for ($at = $resolved; $at -and $at.Length -ge $scratch.Length; $at = Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Linked path: ' + $at)
        }
    }
}
function Get-TreeFiles([string]$Root, [switch]$IncludeRetired) {
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($Root))
    while ($pending.Count) {
        $directory = $pending.Pop()
        if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked directory: ' + $directory) }
        foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked entry: ' + $item.FullName) }
            if (!$IncludeRetired -and $item.Name -in @('UIPreviewsForMe', 'UIPreviewsForMe.meta')) { continue }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) } else { $item }
        }
    }
}
function Assert-Idle {
    $active = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($scratch)
    })
    if ($active.Count) { throw ('Borrowed project referenced by active processes: ' + ($active.ProcessId -join ',')) }
    if (Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')) { throw 'Borrowed project has UnityLockfile.' }
}
function Save-Journal {
    [pscustomobject]@{ Changes=@($changes.ToArray()); AddedDirectories=@($directories.ToArray()) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $journal -Encoding utf8
}
function Record-Target([string]$Target) {
    Assert-Safe $Target
    if (@($changes | Where-Object Target -eq $Target).Count) { return }
    $backup = $null
    if (Test-Path -LiteralPath $Target) {
        $backup = Join-Path $output ('OriginalScratch\' + $Target.Substring($scratch.Length + 1))
        New-Item -ItemType Directory -Force -Path (Split-Path $backup -Parent) | Out-Null
        Copy-Item -LiteralPath $Target -Destination $backup
    }
    $changes.Add([pscustomobject]@{ Target=$Target; Backup=$backup; Added=($null -eq $backup) })
    Save-Journal
}
function Ensure-Directory([string]$Directory) {
    Assert-Safe $Directory
    if (Test-Path -LiteralPath $Directory) { return }
    Ensure-Directory (Split-Path $Directory -Parent)
    $directories.Add($Directory)
    Record-Target ($Directory + '.meta')
    New-Item -ItemType Directory -Path $Directory | Out-Null
    Save-Journal
}
function Load-Journal {
    $saved = Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json
    foreach ($entry in @($saved.Changes)) { $changes.Add($entry) }
    foreach ($directory in @($saved.AddedDirectories)) { $directories.Add($directory) }
}

if ($Stage -eq 'Status') {
    Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($scratch)
    } | Select-Object ProcessId,CommandLine
    $log = Join-Path $output ($Run + '\unity.log')
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 22 }
    exit
}
Assert-Idle
Assert-Safe (Join-Path $scratch 'Assets')
Assert-Safe (Join-Path $scratch 'Library')
$settings = [IO.File]::ReadAllText((Join-Path $scratch 'ProjectSettings\ProjectSettings.asset'))
if ($settings -notmatch 'companyName: CodexCardSupplyValidation' -or $settings -notmatch 'productName: CardSupplyFrames20261021') {
    throw 'Borrowed project must retain its isolated PlayerPrefs identity.'
}
if ($Stage -eq 'Prepare') {
    if (@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File).Count) { throw 'Borrowed project has another task lease.' }
    if (Test-Path -LiteralPath $journal) { throw 'Existing task journal must be preserved.' }
    if (!(Test-Path -LiteralPath (Join-Path $scratch 'Library'))) { throw 'Expected the existing shared warm Library.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $leaseStream = [IO.File]::Open($lease, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $leaseBytes = [Text.Encoding]::UTF8.GetBytes($output)
        $leaseStream.Write($leaseBytes, 0, $leaseBytes.Length)
    } finally { $leaseStream.Dispose() }
    if (@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File | Where-Object FullName -ne $lease).Count) {
        Remove-Item -LiteralPath $lease -Force
        throw 'Another task acquired a lease during preparation; no borrowed inputs were changed.'
    }
    Save-Journal
    [pscustomobject]@{ Project=$scratch; HadTemp=(Test-Path -LiteralPath (Join-Path $scratch 'Temp')); StartedAtUtc=[DateTime]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'preparation.json') -Encoding utf8
} else {
    if (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output) { throw 'Task lease missing or mismatched.' }
    Load-Journal
}

if ($Stage -in @('Prepare','Sync')) {
    $hashPath = Join-Path $output ('source-hashes-' + $Run + '.csv')
    if (Test-Path -LiteralPath $hashPath) { throw 'Use a fresh run name to retain previous input hashes.' }
    $selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    # Source-only synchronization avoids copying any Unity cache or unrelated art.
    foreach ($file in Get-TreeFiles (Join-Path $main 'Assets') | Where-Object { $_.Extension -in @('.cs','.asmdef','.asmref') }) {
        [void]$selected.Add($file.FullName)
        if (Test-Path -LiteralPath ($file.FullName + '.meta')) { [void]$selected.Add($file.FullName + '.meta') }
    }
    foreach ($relative in @('Assets\_Project\Resources\BoardingBattle','Assets\_Project\Resources\Shaders','Assets\_Project\Resources\NavalTactics','Assets\_Project\Resources\ComponentShopV3','Assets\_Project\Resources\ComponentShopRegional','Assets\_Project\Resources\EncounterIllustrations','Assets\_Project\Resources\BattleIcons','Assets\_Project\Resources\PortFacilities\Regional\Voyage','Assets\_Project\Resources\NavigationLookout','Assets\_Project\Resources\BattleVfx','Assets\_Project\Resources\ComicPanelsHD','Assets\_Project\Resources\LegendaryFleetComics','Assets\_Project\Resources\Fonts','Assets\_Project\Resources\OpeningComics','Assets\_Project\Resources\ArrivalComics','Assets\_Project\Resources\BattleComics')) {
        $root = Join-Path $main $relative
        if (Test-Path -LiteralPath $root) { foreach ($file in Get-TreeFiles $root) { [void]$selected.Add($file.FullName) } }
    }
    # Complete visible chrome and paper dependencies for actual scene captures.
    foreach ($resource in @('UI','HeaderFacilityIcons','PortIcons','PortHeaders','UISurfaces','UIWood','MainMenu','AchievementIcons','ItemIcons','PortFacilities\Market')) {
        $root = Join-Path $main ('Assets\_Project\Resources\' + $resource)
        if (Test-Path -LiteralPath $root) { foreach ($file in Get-TreeFiles $root) { [void]$selected.Add($file.FullName) } }
    }
    # The warm project already has the remaining gameplay audio. This newer
    # menu/port track is required by each real-scene PlayMode fixture.
    foreach ($relative in @('Assets\_Project\Resources\Audio\Music\the_captains_vow.wav')) {
        $source = Join-Path $main $relative
        foreach ($path in @($source, ($source + '.meta'))) {
            if (!(Test-Path -LiteralPath $path)) { throw ('Required PlayMode audio missing: ' + $path) }
            [void]$selected.Add($path)
        }
    }
    foreach ($name in @('manifest.json','packages-lock.json')) {
        $path = Join-Path $main ('Packages\' + $name)
        if (Test-Path -LiteralPath $path) { [void]$selected.Add($path) }
    }
    # A superseded task implementation may remove source/assets from main.
    # Remove only previously synchronized files that no longer exist there;
    # their pre-task originals are still covered by the restoration journal.
    $previousHashes = Join-Path $output 'source-hashes.csv'
    if (Test-Path -LiteralPath $previousHashes) {
        foreach ($previous in Import-Csv -LiteralPath $previousHashes) {
            if (Test-Path -LiteralPath $previous.Path) { continue }
            $target = Join-Path $scratch $previous.Relative
            Assert-Safe $target
            if (!(Test-Path -LiteralPath $target)) { continue }
            if ((Get-FileHash -LiteralPath $target).Hash -ne $previous.SHA256) {
                throw ('Removed source has a separately changed scratch copy: ' + $target)
            }
            Record-Target $target
            Remove-Item -LiteralPath $target -Force
        }
    }
    foreach ($file in @($selected)) {
        for ($parent = Split-Path $file -Parent; $parent.StartsWith($main + '\Assets\', [StringComparison]::OrdinalIgnoreCase); $parent = Split-Path $parent -Parent) {
            if (Test-Path -LiteralPath ($parent + '.meta')) { [void]$selected.Add($parent + '.meta') }
        }
    }
    $inputs = foreach ($path in $selected | Sort-Object) {
        $relative = $path.Substring($main.Length + 1)
        $target = Join-Path $scratch $relative
        Assert-Safe $target
        $file = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if (!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) {
            Record-Target $target
            Ensure-Directory (Split-Path $target -Parent)
            Copy-Item -LiteralPath $path -Destination $target -Force
        }
        [pscustomobject]@{ Path=$path; Relative=$relative; SHA256=$hash; Length=$file.Length }
    }
    # Preserve and quarantine obsolete scratch-only helpers so they cannot compile
    # against a different historical runtime. Cleanup restores them byte for byte.
    foreach ($file in @(Get-TreeFiles (Join-Path $scratch 'Assets') | Where-Object Extension -eq '.cs')) {
        $relative = $file.FullName.Substring($scratch.Length + 1)
        if (Test-Path -LiteralPath (Join-Path $main $relative)) { continue }
        foreach ($path in @($file.FullName, ($file.FullName + '.meta'))) {
            if (!(Test-Path -LiteralPath $path)) { continue }
            Record-Target $path
            Remove-Item -LiteralPath $path -Force
        }
    }
    # Also retire metadata left behind by older copies of the removed scripts.
    foreach ($relative in @(
        'Assets\_Project\Presentation\Runtime\TreasureComicDialogueCatalog.cs.meta',
        'Assets\_Project\Presentation\Runtime\TreasureComicDialogueLayout.cs.meta',
        'Assets\_Project\Tests\EditMode\TreasureComicDialogueLayoutTests.cs.meta'
    )) {
        $target = Join-Path $scratch $relative
        Assert-Safe $target
        if (Test-Path -LiteralPath $target) {
            Record-Target $target
            Remove-Item -LiteralPath $target -Force
        }
    }
    # The removed treasure-entry comics must not be supplied by this old warm
    # project. Journal every original file, leave directories for restoration,
    # and validate against an empty resource directory without touching main.
    $removedComics = Join-Path $scratch 'Assets\_Project\Resources\TreasureHunt\Comics'
    Assert-Safe $removedComics
    if (Test-Path -LiteralPath $removedComics) {
        foreach ($file in @(Get-TreeFiles $removedComics)) {
            Record-Target $file.FullName
            Remove-Item -LiteralPath $file.FullName -Force
        }
    }
    $inputs | Export-Csv -LiteralPath $hashPath -NoTypeInformation -Encoding utf8
    Copy-Item -LiteralPath $hashPath -Destination (Join-Path $output 'source-hashes.csv') -Force
    Write-Output ('Prepared ' + $inputs.Count + ' source/meta/comic inputs; ' + $changes.Count + ' borrowed files journaled; Unity not started.')
    exit
}

if ($Stage -eq 'Test') {
    $removedComics = Join-Path $scratch 'Assets\_Project\Resources\TreasureHunt\Comics'
    Assert-Safe $removedComics
    if ((Test-Path -LiteralPath $removedComics) -and @(Get-TreeFiles $removedComics | Where-Object Extension -ne '.meta').Count) {
        throw 'Retired treasure-entry comic resources remain in the validation project.'
    }
    $testOutput = Join-Path $output $Run
    if (Test-Path -LiteralPath $testOutput) { throw 'Use a fresh test run name.' }
    $comicRoot = Join-Path $scratch 'Assets\_Project\Resources\LegendaryFleetComics'
    foreach ($ship in @('iron_oath','thunder_crown','red_widow','ember_ark','tide_requiem','last_horizon')) {
        foreach ($phase in @('before','after')) {
            if (!(Test-Path -LiteralPath (Join-Path $comicRoot ($ship + '_' + $phase + '.png')))) { throw ('Missing comic: ' + $ship + '_' + $phase) }
        }
    }
    foreach ($index in 1..4) {
        $path = Join-Path $scratch ('Assets\_Project\Resources\OpeningComics\opening_page0' + $index + '.png')
        if (!(Test-Path -LiteralPath $path)) { throw ('Missing opening comic: ' + $path) }
    }
    foreach ($port in @('white_shell','bronze_bell','salt_crown','twilight_tide','gray_gull')) {
        foreach ($index in 1..2) {
            $path = Join-Path $scratch ('Assets\_Project\Resources\ArrivalComics\' + $port + '_page0' + $index + '.png')
            if (!(Test-Path -LiteralPath $path)) { throw ('Missing arrival comic: ' + $path) }
        }
    }
    New-Item -ItemType Directory -Path $testOutput | Out-Null
    $arguments = @('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform',$TestPlatform,
        '-testFilter',$TestFilter,
        '-navalVisualCapturePath',(Join-Path $testOutput 'Artifacts\Battle'),'-testResults',(Join-Path $testOutput ($TestPlatform + '.xml')),'-logFile',(Join-Path $testOutput 'unity.log'))
    $previousCapture = $env:COMIC_PAGINATION_CAPTURE_DIR
    $previous4K = $env:COMIC_CAPTURE_4K
    try {
        $env:COMIC_PAGINATION_CAPTURE_DIR = Join-Path $testOutput 'screenshots'
        $env:COMIC_CAPTURE_4K = if ($Capture4K) { '1' } else { '0' }
        $process = Start-Process -FilePath $unity -ArgumentList ($arguments | ForEach-Object { '"' + $_ + '"' }) -WindowStyle Hidden -PassThru
        [pscustomobject]@{ PID=$process.Id; Project=$scratch; Output=$testOutput; Arguments=$arguments; StartedAtUtc=[DateTime]::UtcNow.ToString('O') } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $testOutput 'launch.json') -Encoding utf8
        Write-Output ('Started isolated ' + $TestPlatform + ' PID ' + $process.Id + '; ' + $testOutput)
    } finally { $env:COMIC_PAGINATION_CAPTURE_DIR = $previousCapture; $env:COMIC_CAPTURE_4K = $previous4K }
    exit
}

$inputs = @(Import-Csv -LiteralPath (Join-Path $output 'source-hashes.csv'))
$beforeCleanup = foreach ($entry in $inputs) {
    [pscustomobject]@{ Path=$entry.Path; SHA256=if(Test-Path -LiteralPath $entry.Path){(Get-FileHash -LiteralPath $entry.Path).Hash}else{'MISSING'} }
}
$beforeCleanup | Export-Csv -LiteralPath (Join-Path $output 'pre-cleanup-main-hashes.csv') -NoTypeInformation -Encoding utf8
# Keep the exact validated text sources alongside logs; source evidence is not a cache.
foreach ($entry in $inputs | Where-Object { $_.Relative -match '\.(cs|asmdef|asmref)$' }) {
    $source = Join-Path $scratch $entry.Relative
    if (!(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source).Hash -ne $entry.SHA256) { throw ('Validated input changed: ' + $source) }
    $snapshot = Join-Path $output ('validated-source\' + $entry.Relative)
    New-Item -ItemType Directory -Force -Path (Split-Path $snapshot -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $snapshot
}
$freed = 0L
foreach ($change in $changes) {
    Assert-Safe $change.Target
    if ($change.Added) {
        if (Test-Path -LiteralPath $change.Target) {
            $freed += (Get-Item -LiteralPath $change.Target).Length
            Remove-Item -LiteralPath $change.Target -Force
        }
        if (Test-Path -LiteralPath $change.Target) { throw ('Addition remains: ' + $change.Target) }
    } else {
        Copy-Item -LiteralPath $change.Backup -Destination $change.Target -Force
        if ((Get-FileHash -LiteralPath $change.Backup).Hash -ne (Get-FileHash -LiteralPath $change.Target).Hash) { throw ('Restore failed: ' + $change.Target) }
    }
}
foreach ($directory in $directories | Sort-Object Length -Descending) {
    Assert-Safe $directory
    if ((Test-Path -LiteralPath $directory) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory -Force }
}
$preparation = Get-Content -LiteralPath (Join-Path $output 'preparation.json') -Raw | ConvertFrom-Json
$temp = Join-Path $scratch 'Temp'
if ((Test-Path -LiteralPath $temp) -and !$preparation.HadTemp) {
    Assert-Safe $temp
    $files = @(Get-TreeFiles $temp -IncludeRetired)
    $freed += ($files | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $temp -Recurse -Force
    if (Test-Path -LiteralPath $temp) { throw 'Task Temp cleanup failed.' }
}
# Restored inputs now live in the borrowed project; remove only this task's
# verified duplicate restore backups, preserving journals and validated sources.
$backupRoot = [IO.Path]::GetFullPath((Join-Path $output 'OriginalScratch'))
if (!$backupRoot.StartsWith([IO.Path]::GetFullPath($output) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup path escaped task output.' }
if (Test-Path -LiteralPath $backupRoot) {
    for ($at = $backupRoot; $at -and $at.Length -ge 'D:\Game'.Length; $at = Split-Path $at -Parent) {
        if ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked backup ancestor: ' + $at) }
    }
    $backupItems = @(Get-ChildItem -LiteralPath $backupRoot -Recurse -Force)
    if (@($backupItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Linked restore backup.' }
    $freed += ($backupItems | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $backupRoot -Recurse -Force
    if (Test-Path -LiteralPath $backupRoot) { throw 'Verified backup cleanup failed.' }
}
$duringCleanup = @($beforeCleanup | Where-Object {
    $current = if(Test-Path -LiteralPath $_.Path){(Get-FileHash -LiteralPath $_.Path).Hash}else{'MISSING'}
    $current -ne $_.SHA256
})
$sinceSnapshot = @($inputs | Where-Object { !(Test-Path -LiteralPath $_.Path) -or (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 })
[pscustomobject]@{ ComparedInputs=$inputs.Count; DifferencesDuringCleanup=$duringCleanup.Count; ConcurrentChangesSinceSnapshot=$sinceSnapshot.Count; ReclaimedBytes=$freed; SharedLibraryRetained=$true } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'cleanup-verification.json') -Encoding utf8
if ($duringCleanup.Count) { throw 'Main inputs changed during cleanup; inspect retained hashes.' }
Remove-Item -LiteralPath $lease -Force
@('Borrowed source inputs restored and verified. Main UnityClient and its Library were never written by this runner.',
    ('Reclaimed bytes: ' + $freed), ('Pre-existing Temp retained: ' + $preparation.HadTemp),
    'Shared Library retained: used by ComponentRarityHalo20261019, CardSupplyFrames20261021, AchievementBalance20261022 and BattleResourceIcons20261026.',
    'Validated text sources, journals, hashes, XML results, Unity log and screenshots retained; redundant restore backups removed.') |
    Set-Content -LiteralPath (Join-Path $output 'cleanup.txt') -Encoding utf8
Write-Output ('Cleanup complete; reclaimed bytes ' + $freed + '; source differences during cleanup ' + $duringCleanup.Count)
