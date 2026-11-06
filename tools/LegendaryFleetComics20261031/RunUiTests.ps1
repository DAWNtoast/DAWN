param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Sync','Test','Status','Cleanup')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run = 'run1'
)
$ErrorActionPreference = 'Stop'
$main = 'D:\Game\UnityClient'
$scratch = 'D:\Game\tmp\component-rarity-halo-20261019'
$output = 'D:\Game\outputs\legendary-comics-20261031\ui-validation'
$journal = Join-Path $output 'scratch-journal.json'
$lease = Join-Path $scratch 'LegendaryFleetComics20261031.lock'
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
    [IO.File]::WriteAllText($lease, $output)
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
    foreach ($relative in @('Assets\_Project\Resources\LegendaryFleetComics','Assets\_Project\Resources\TreasureHunt\Comics','Assets\_Project\Resources\Fonts')) {
        $root = Join-Path $main $relative
        if (Test-Path -LiteralPath $root) { foreach ($file in Get-TreeFiles $root) { [void]$selected.Add($file.FullName) } }
    }
    foreach ($name in @('manifest.json','packages-lock.json')) {
        $path = Join-Path $main ('Packages\' + $name)
        if (Test-Path -LiteralPath $path) { [void]$selected.Add($path) }
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
        foreach ($path in @($file.FullName, $file.FullName + '.meta')) {
            if (!(Test-Path -LiteralPath $path)) { continue }
            Record-Target $path
            Remove-Item -LiteralPath $path -Force
        }
    }
    $inputs | Export-Csv -LiteralPath $hashPath -NoTypeInformation -Encoding utf8
    Copy-Item -LiteralPath $hashPath -Destination (Join-Path $output 'source-hashes.csv') -Force
    Write-Output ('Prepared ' + $inputs.Count + ' source/meta/comic inputs; ' + $changes.Count + ' borrowed files journaled; Unity not started.')
    exit
}

if ($Stage -eq 'Test') {
    $testOutput = Join-Path $output $Run
    if (Test-Path -LiteralPath $testOutput) { throw 'Use a fresh test run name.' }
    $comicRoot = Join-Path $scratch 'Assets\_Project\Resources\LegendaryFleetComics'
    foreach ($ship in @('iron_oath','thunder_crown','red_widow','ember_ark','tide_requiem','last_horizon')) {
        foreach ($phase in @('before','after')) {
            if (!(Test-Path -LiteralPath (Join-Path $comicRoot ($ship + '_' + $phase + '.png')))) { throw ('Missing comic: ' + $ship + '_' + $phase) }
        }
    }
    New-Item -ItemType Directory -Path $testOutput | Out-Null
    $arguments = @('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform','EditMode',
        '-testFilter','LegendaryFleetComicPlaybackTests;LegendaryFleetComicCatalogTests;TreasureComicDialogueLayoutTests',
        '-testResults',(Join-Path $testOutput 'EditMode.xml'),'-logFile',(Join-Path $testOutput 'unity.log'))
    $previousCapture = $env:LEGENDARY_COMIC_CAPTURE_DIR
    try {
        $env:LEGENDARY_COMIC_CAPTURE_DIR = Join-Path $testOutput 'screenshots'
        $process = Start-Process -FilePath $unity -ArgumentList ($arguments | ForEach-Object { '"' + $_ + '"' }) -WindowStyle Hidden -PassThru
        [pscustomobject]@{ PID=$process.Id; Project=$scratch; Output=$testOutput; Arguments=$arguments; StartedAtUtc=[DateTime]::UtcNow.ToString('O') } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $testOutput 'launch.json') -Encoding utf8
        Write-Output ('Started isolated EditMode PID ' + $process.Id + '; ' + $testOutput)
    } finally { $env:LEGENDARY_COMIC_CAPTURE_DIR = $previousCapture }
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
    'Input backups, validated text sources, hashes, XML results, Unity log and screenshots retained.') |
    Set-Content -LiteralPath (Join-Path $output 'cleanup.txt') -Encoding utf8
Write-Output ('Cleanup complete; reclaimed bytes ' + $freed + '; source differences during cleanup ' + $duringCleanup.Count)
