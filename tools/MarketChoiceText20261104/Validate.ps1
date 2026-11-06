param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Sync','Test','Capture','Status','Cleanup')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run = 'run1',
    [ValidateSet('EditMode','PlayMode')][string]$TestPlatform = 'EditMode',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet = 'validation',
    [string]$TestFilter = 'WorkshopPageNavigationTests',
    [string[]]$RelativePaths = @()
)
$ErrorActionPreference = 'Stop'
$workspace = 'D:\Game'
$main = Join-Path $workspace 'UnityClient'
$scratch = Join-Path $workspace 'tmp\shipyard-assembly-20261017'
$output = Join-Path $workspace ('outputs\market-choice-text-20261104\' + $EvidenceSet)
$journal = Join-Path $output 'scratch-journal.json'
$lease = Join-Path $scratch 'MarketChoiceText20261104.lock'
$unity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$changes = [Collections.Generic.List[object]]::new()
function Assert-Safe([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith($scratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw ('Outside borrowed project: ' + $resolved) }
    for ($at=$resolved; $at -and $at.Length -ge $scratch.Length; $at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked path: ' + $at) }
    }
}
function Assert-Idle {
    $active = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch) })
    if ($active.Count) { throw ('Borrowed project occupied by Unity: ' + ($active.ProcessId -join ',')) }
    if (Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')) { throw 'Borrowed project has UnityLockfile.' }
}
function Save-Journal { ConvertTo-Json -InputObject @($changes.ToArray()) -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8 }
function Save-RunSources([string]$Destination) {
    $manifest=@(foreach($entry in Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') | Where-Object { $_.Path -match '(CoreGameplayMarketStorage|MarketStorageFlowTests)\.cs$' }) {
        $relative=$entry.Path.Substring($main.Length+1)
        $source=Join-Path $scratch $relative; Assert-Safe $source
        $target=Join-Path $Destination ('SourceSnapshot\'+$relative)
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
        [pscustomobject]@{Path=$relative;SHA256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash}
    })
    $manifest | Export-Csv -LiteralPath (Join-Path $Destination 'source-manifest.csv') -NoTypeInformation -Encoding utf8
}
function Record-Target([string]$Target) {
    Assert-Safe $Target
    if (@($changes | Where-Object Target -eq $Target).Count) { return }
    $backup=$null
    if (Test-Path -LiteralPath $Target) {
        $relative=$Target.Substring($scratch.Length+1)
        $backup=Join-Path $output ('OriginalScratch\'+$relative)
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $Target -Destination $backup
    }
    $changes.Add([pscustomobject]@{Target=$Target;Backup=$backup;Added=($null -eq $backup)})
    Save-Journal
}
if ($Stage -eq 'Status') {
    Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch) } | Select-Object ProcessId,CommandLine
    $log=Join-Path $output ($Run+'\unity.log')
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 20 }
    exit
}
Assert-Idle
Assert-Safe (Join-Path $scratch 'Assets')
Assert-Safe (Join-Path $scratch 'Library')
$settings=[IO.File]::ReadAllText((Join-Path $scratch 'ProjectSettings\ProjectSettings.asset'))
if ($settings -notmatch 'companyName: CodexShipyardAssemblyValidation' -or $settings -notmatch 'productName: ShipyardAssemblyValidation20261017') { throw 'Borrowed project must retain isolated PlayerPrefs identity.' }
if ($Stage -eq 'Prepare' -or $Stage -eq 'Sync') {
    if ($Stage -eq 'Prepare') {
    if (@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File).Count) { throw 'Borrowed project has a task lease; preserve it.' }
    if (Test-Path -LiteralPath $journal) { throw 'Existing task journal must be preserved.' }
    if (!(Test-Path -LiteralPath (Join-Path $scratch 'Library'))) { throw 'Expected existing shared warm Library.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    [IO.File]::WriteAllText($lease,$output)
    Save-Journal
    [pscustomobject]@{Project=$scratch;HadTemp=(Test-Path -LiteralPath (Join-Path $scratch 'Temp'));StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'preparation.json') -Encoding utf8
    } else {
        if (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output) { throw 'Task lease missing or mismatched.' }
        foreach($entry in @(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)) { $changes.Add($entry) }
        $prior=Join-Path $output ('main-source-hashes-before-'+$Run+'.csv')
        if(Test-Path -LiteralPath $prior){throw 'Choose a fresh Sync run name to preserve source history.'}
        Copy-Item -LiteralPath (Join-Path $output 'main-source-hashes.csv') -Destination $prior
    }
    $inputs=[Collections.Generic.List[object]]::new()
    if ($RelativePaths.Count -and $Stage -ne 'Sync') { throw 'Scoped paths are only valid with Sync.' }
    if ($RelativePaths.Count) {
        foreach($entry in @(Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv'))) {$inputs.Add($entry)}
        $sourceFiles=@(foreach($relative in $RelativePaths) {
            $candidate=[IO.Path]::GetFullPath((Join-Path $main $relative))
            if (!$candidate.StartsWith($main+'\Assets\',[StringComparison]::OrdinalIgnoreCase) -and !$candidate.StartsWith($main+'\Packages\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Scoped source is outside Assets/Packages.'}
            Get-Item -LiteralPath $candidate
        })
    } else {
        $sourceFiles=@(foreach($part in @('Assets','Packages')) {Get-ChildItem -LiteralPath (Join-Path $main $part) -File -Recurse})
    }
        foreach ($file in $sourceFiles) {
            if ($file.FullName -like '*\UIPreviewsForMe*') { continue }
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked input: '+$file.FullName) }
            $relative=$file.FullName.Substring($main.Length+1)
            $target=Join-Path $scratch $relative
            Assert-Safe $target
            $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if($RelativePaths.Count){for($i=$inputs.Count-1;$i -ge 0;$i--){if($inputs[$i].Path -eq $file.FullName){$inputs.RemoveAt($i)}}}
            $inputs.Add([pscustomobject]@{Path=$file.FullName;SHA256=$hash;Length=$file.Length})
            if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash) { continue }
            Record-Target $target
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    # Quarantine stale scratch-only C# helpers outside Assets; restore them in Cleanup.
    if (!$RelativePaths.Count) { foreach ($file in Get-ChildItem -LiteralPath (Join-Path $scratch 'Assets') -Filter '*.cs' -File -Recurse) {
        if ($file.FullName -like '*\UIPreviewsForMe*') { continue }
        $relative=$file.FullName.Substring($scratch.Length+1)
        if (Test-Path -LiteralPath (Join-Path $main $relative)) { continue }
        Record-Target $file.FullName
        Remove-Item -LiteralPath $file.FullName -Force
    } }
    $captureSource=Join-Path $PSScriptRoot 'Capture.cs'
    $captureTarget=Join-Path $scratch 'Assets\_Project\Editor\MarketChoiceTextCapture20261104.cs'
    Record-Target $captureTarget
    Record-Target ($captureTarget+'.meta')
    Copy-Item -LiteralPath $captureSource -Destination $captureTarget -Force
    if (!(Test-Path -LiteralPath ($captureTarget+'.meta'))) {
        [IO.File]::WriteAllText(($captureTarget+'.meta'), "fileFormatVersion: 2`nguid: c234a1e6775b4283a27094617743d1fa`n", [Text.UTF8Encoding]::new($false))
    }
    $inputs | Export-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') -NoTypeInformation -Encoding utf8
    Write-Output ('Prepared '+$changes.Count+' changed files; no new project/Library; Unity not started.')
    exit
}
if (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output) { throw 'Task lease missing or mismatched.' }
$changes=@(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)
if ($Stage -eq 'Test' -or $Stage -eq 'Capture') {
    $testOutput=Join-Path $output $Run
    if (Test-Path -LiteralPath $testOutput) { throw 'Choose a fresh test run name.' }
    New-Item -ItemType Directory -Path $testOutput | Out-Null
    Save-RunSources $testOutput
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform',$TestPlatform,'-testFilter',$TestFilter,'-testResults',(Join-Path $testOutput ($TestPlatform+'.xml')),'-logFile',(Join-Path $testOutput 'unity.log'))
    if ($Stage -eq 'Test' -and $TestPlatform -eq 'PlayMode') { $arguments += @('-navalVisualCapturePath',(Join-Path $testOutput 'Artifacts')) }
    if ($Stage -eq 'Capture') {
        $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-executeMethod','ShatteredSail.Editor.MarketChoiceTextCapture20261104.Run','-marketChoiceTextOutput',$testOutput,'-logFile',(Join-Path $testOutput 'unity.log'),'-quit')
    }
    $process=Start-Process -FilePath $unity -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Project=$scratch;Output=$testOutput;Arguments=$arguments;StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $testOutput 'launch.json') -Encoding utf8
    Write-Output (('Started '+$TestPlatform+' PID ')+$process.Id+'; '+$testOutput)
    exit
}
$preCleanupHashes=@(Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') | ForEach-Object {
    [pscustomobject]@{Path=$_.Path;SHA256=if(Test-Path -LiteralPath $_.Path){(Get-FileHash -LiteralPath $_.Path).Hash}else{'MISSING'}}
})
$preCleanupHashes | Export-Csv -LiteralPath (Join-Path $output 'pre-cleanup-main-hashes.csv') -NoTypeInformation -Encoding utf8
$freed=0L
$restoreEvidence=[Collections.Generic.List[object]]::new()
foreach ($change in $changes) {
    Assert-Safe $change.Target
    if ($change.Added) {
        if (Test-Path -LiteralPath $change.Target) { $freed+=(Get-Item -LiteralPath $change.Target).Length; Remove-Item -LiteralPath $change.Target -Force }
        if (Test-Path -LiteralPath $change.Target) { throw 'Task addition was not removed.' }
        $restoreEvidence.Add([pscustomobject]@{Target=$change.Target;Result='AddedFileRemoved';ExpectedSHA256='ABSENT';RestoredSHA256='ABSENT'})
    } else {
        $expectedHash=(Get-FileHash -LiteralPath $change.Backup).Hash
        Copy-Item -LiteralPath $change.Backup -Destination $change.Target -Force
        $restoredHash=(Get-FileHash -LiteralPath $change.Target).Hash
        if ($expectedHash -ne $restoredHash) { throw 'Borrowed source restore failed.' }
        $restoreEvidence.Add([pscustomobject]@{Target=$change.Target;Result='OriginalRestored';ExpectedSHA256=$expectedHash;RestoredSHA256=$restoredHash})
    }
}
$restoreEvidence | Export-Csv -LiteralPath (Join-Path $output 'restoration-verification.csv') -NoTypeInformation -Encoding utf8
# Backups are task-created duplicates. After every borrowed target is restored and
# verified, the original bytes live in the borrowed project again; retain only the
# final executed source snapshots and the journal/hashes as evidence.
$restoreBackups=[IO.Path]::GetFullPath((Join-Path $output 'OriginalScratch'))
if (!$restoreBackups.StartsWith([IO.Path]::GetFullPath($output)+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Restore backup path escaped this task output.'}
if (Test-Path -LiteralPath $restoreBackups) {
    for($at=$restoreBackups;$at -and $at.Length -ge $workspace.Length;$at=Split-Path $at -Parent) {
        if ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {throw 'Restore backup has linked ancestor.'}
    }
    $backupItems=@(Get-ChildItem -LiteralPath $restoreBackups -Recurse -Force)
    if (@($backupItems|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count) {throw 'Restore backup contains linked paths.'}
    $freed+=($backupItems|Where-Object {!$_.PSIsContainer}|Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $restoreBackups -Recurse -Force
    if (Test-Path -LiteralPath $restoreBackups) {throw 'Verified restore backup cleanup failed.'}
}
$prep=Get-Content -LiteralPath (Join-Path $output 'preparation.json') -Raw | ConvertFrom-Json
$temp=Join-Path $scratch 'Temp'
if ((Test-Path -LiteralPath $temp) -and !$prep.HadTemp) {
    Assert-Safe $temp
    $items=@(Get-ChildItem -LiteralPath $temp -Recurse -Force)
    if (@($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Task Temp contains linked paths.' }
    $freed+=($items | Where-Object {!$_.PSIsContainer} | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $temp -Recurse -Force
    if (Test-Path -LiteralPath $temp) { throw 'Task Temp cleanup failed.' }
}
$mainChanges=@(Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') | Where-Object { !(Test-Path -LiteralPath $_.Path) -or (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 })
$cleanupMainChanges=@($preCleanupHashes | Where-Object { $current=if(Test-Path -LiteralPath $_.Path){(Get-FileHash -LiteralPath $_.Path).Hash}else{'MISSING'}; $current -ne $_.SHA256 })
[pscustomobject]@{ComparedInputs=$preCleanupHashes.Count;DifferencesDuringCleanup=$cleanupMainChanges.Count;UnrelatedChangesSincePreparation=$mainChanges.Count;ReclaimedBytes=$freed} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'cleanup-verification.json') -Encoding utf8
if($cleanupMainChanges.Count){$cleanupMainChanges | Export-Csv -LiteralPath (Join-Path $output 'changes-during-cleanup.csv') -NoTypeInformation -Encoding utf8}
Remove-Item -LiteralPath $lease -Force
@('Borrowed project source restored and verified.','No files inside the main UnityClient project were written by validation or cleanup.',('Main source differences since snapshot: '+$mainChanges.Count),('Bytes reclaimed: '+$freed),('Pre-existing Temp retained: '+$prep.HadTemp),'Shared Library retained: used by ShipyardAssembly20261017, CardWorkshopPages20261031 and existing UI validation tools.','Task scripts, executed source snapshots, journal/hashes, logs and screenshots retained; verified redundant restore backups removed.') | Set-Content -LiteralPath (Join-Path $output 'cleanup.txt') -Encoding utf8
if ($mainChanges.Count) { $mainChanges | Export-Csv -LiteralPath (Join-Path $output 'concurrent-main-source-changes.csv') -NoTypeInformation -Encoding utf8 }
Write-Output ('Cleanup complete, bytes reclaimed '+$freed+', concurrent main differences '+$mainChanges.Count)
