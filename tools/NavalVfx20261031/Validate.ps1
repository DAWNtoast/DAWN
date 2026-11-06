param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Sync','Capture','Test','Status','Cleanup')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run = 'run1',
    [ValidateSet('EditMode','PlayMode')][string]$TestPlatform = 'EditMode',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet = 'validation',
    [string[]]$SyncFiles = @(),
    [ValidateRange(24,96)][int]$FrameCount = 60,
    [string]$TestFilter = 'NavalCombatFxGraphicTests;NavalCardVfxPlanTests;CoreNavalResolvedAttackTests;BattleFxGraphicTests;BattleSchoolAttackGraphicTests;NavalFieldMotionTests'
)
$ErrorActionPreference = 'Stop'
$workspace = 'D:\Game'
$main = Join-Path $workspace 'UnityClient'
$scratch = Join-Path $workspace 'tmp\shipyard-assembly-20261017'
$output = Join-Path $workspace ('outputs\naval-vfx-20261031\' + $EvidenceSet)
$journal = Join-Path $output 'scratch-journal.json'
$lease = Join-Path $scratch 'NavalVfx20261031.lock'
$unity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$changes = [Collections.Generic.List[object]]::new()
function Assert-Safe([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith($scratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw ('Outside borrowed project: ' + $resolved) }
    for ($at=$resolved; $at -and $at.Length -ge $scratch.Length; $at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked path: ' + $at) }
    }
}
function Get-TreeFiles([string]$Root) {
    # Explicit descent rejects links before traversing them and never enters retired previews.
    $pending=[Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($Root))
    while($pending.Count) {
        $directory=$pending.Pop()
        $directoryItem=Get-Item -LiteralPath $directory -Force
        if($directoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked tree: '+$directory) }
        foreach($item in Get-ChildItem -LiteralPath $directory -Force) {
            if($item.Name -eq 'UIPreviewsForMe' -or $item.Name -eq 'UIPreviewsForMe.meta') { continue }
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked tree entry: '+$item.FullName) }
            if($item.PSIsContainer) { $pending.Push($item.FullName) } else { $item }
        }
    }
}
function Assert-Idle {
    $active = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch) })
    if ($active.Count) { throw ('Borrowed project occupied by Unity: ' + ($active.ProcessId -join ',')) }
    if (Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')) { throw 'Borrowed project has UnityLockfile.' }
}
function Save-Journal { ConvertTo-Json -InputObject @($changes.ToArray()) -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8 }
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
    $selectedFiles=@()
    if($Stage -eq 'Sync' -and $SyncFiles.Count) {
        foreach($entry in Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv')) { $inputs.Add($entry) }
        foreach($relativeInput in $SyncFiles) {
            $inputPath=[IO.Path]::GetFullPath((Join-Path $main $relativeInput))
            if(!$inputPath.StartsWith((Join-Path $main 'Assets\_Project\'),[StringComparison]::OrdinalIgnoreCase) -or
                $inputPath -match 'UIPreviewsForMe' -or $inputPath -notmatch '\.(cs|meta)$') { throw ('Targeted Sync requires a production source/meta: '+$inputPath) }
            $selectedFiles+=Get-Item -LiteralPath $inputPath -Force
        }
    } else {
        foreach($part in @('Assets','Packages')) { $selectedFiles+=@(Get-TreeFiles (Join-Path $main $part)) }
    }
        foreach ($file in $selectedFiles) {
            if ($file.FullName -like '*\UIPreviewsForMe*') { continue }
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked input: '+$file.FullName) }
            $relative=$file.FullName.Substring($main.Length+1)
            $target=Join-Path $scratch $relative
            Assert-Safe $target
            $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $existing=@($inputs | Where-Object Path -eq $file.FullName)
            foreach($entry in $existing) { [void]$inputs.Remove($entry) }
            $inputs.Add([pscustomobject]@{Path=$file.FullName;SHA256=$hash;Length=$file.Length})
            if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash) { continue }
            Record-Target $target
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    # Quarantine stale scratch-only C# helpers outside Assets; restore them in Cleanup.
    if(!$SyncFiles.Count) { foreach ($file in @(Get-TreeFiles (Join-Path $scratch 'Assets') | Where-Object Extension -eq '.cs')) {
        if ($file.FullName -like '*\UIPreviewsForMe*') { continue }
        $relative=$file.FullName.Substring($scratch.Length+1)
        if (Test-Path -LiteralPath (Join-Path $main $relative)) { continue }
        Record-Target $file.FullName
        Remove-Item -LiteralPath $file.FullName -Force
    } }
    $inputs | Export-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') -NoTypeInformation -Encoding utf8
    $target=Join-Path $scratch 'Assets\_Project\Editor\NavalVfxCapture20261031.cs'
    Record-Target $target
    Record-Target ($target+'.meta')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination $target -Force
    Write-Output ('Prepared '+$changes.Count+' changed files; no new project/Library; Unity not started.')
    exit
}
if (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output) { throw 'Task lease missing or mismatched.' }
$changes=@(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)
if ($Stage -eq 'Test') {
    $testOutput=Join-Path $output $Run
    if (Test-Path -LiteralPath $testOutput) { throw 'Choose a fresh test run name.' }
    New-Item -ItemType Directory -Path $testOutput | Out-Null
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform',$TestPlatform,'-testFilter',$TestFilter,'-testResults',(Join-Path $testOutput ($TestPlatform+'.xml')),'-logFile',(Join-Path $testOutput 'unity.log'))
    if ($Stage -eq 'Test' -and $TestPlatform -eq 'PlayMode') { $arguments += @('-navalVisualCapturePath',(Join-Path $testOutput 'Artifacts')) }
    $process=Start-Process -FilePath $unity -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Project=$scratch;Output=$testOutput;Arguments=$arguments;StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $testOutput 'launch.json') -Encoding utf8
    Write-Output (('Started '+$TestPlatform+' PID ')+$process.Id+'; '+$testOutput)
    exit
}
if ($Stage -eq 'Capture') {
    $runOutput=Join-Path $output $Run
    if (Test-Path -LiteralPath $runOutput) { throw 'Choose a fresh run name.' }
    New-Item -ItemType Directory -Path $runOutput | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $scratch 'Assets\_Project\Editor\NavalVfxCapture20261031.cs') -Force
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-executeMethod','ShatteredSail.Editor.NavalVfxCapture20261031.CaptureFromCommandLine','-navalVfxOutput',$runOutput,'-navalVfxEvidenceSet',$EvidenceSet,'-navalVfxFrames',$FrameCount,'-logFile',(Join-Path $runOutput 'unity.log'),'-quit')
    if ($Stage -eq 'Test' -and $TestPlatform -eq 'PlayMode') { $arguments += @('-navalVisualCapturePath',(Join-Path $testOutput 'Artifacts')) }
    $process=Start-Process -FilePath $unity -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Project=$scratch;Output=$runOutput;Arguments=$arguments;StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runOutput 'launch.json') -Encoding utf8
    Write-Output ('Started capture PID '+$process.Id+'; '+$runOutput)
    exit
}
$preCleanupHashes=@(Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') | ForEach-Object {
    [pscustomobject]@{Path=$_.Path;SHA256=if(Test-Path -LiteralPath $_.Path){(Get-FileHash -LiteralPath $_.Path).Hash}else{'MISSING'}}
})
$preCleanupHashes | Export-Csv -LiteralPath (Join-Path $output 'pre-cleanup-main-hashes.csv') -NoTypeInformation -Encoding utf8
$freed=0L
foreach ($change in $changes) {
    Assert-Safe $change.Target
    if ($change.Added) {
        if (Test-Path -LiteralPath $change.Target) { $freed+=(Get-Item -LiteralPath $change.Target).Length; Remove-Item -LiteralPath $change.Target -Force }
        if (Test-Path -LiteralPath $change.Target) { throw 'Task addition was not removed.' }
    } else {
        Copy-Item -LiteralPath $change.Backup -Destination $change.Target -Force
        if ((Get-FileHash -LiteralPath $change.Backup).Hash -ne (Get-FileHash -LiteralPath $change.Target).Hash) { throw 'Borrowed source restore failed.' }
    }
}
$prep=Get-Content -LiteralPath (Join-Path $output 'preparation.json') -Raw | ConvertFrom-Json
$temp=Join-Path $scratch 'Temp'
if ((Test-Path -LiteralPath $temp) -and !$prep.HadTemp) {
    Assert-Safe $temp
    $items=@(Get-TreeFiles $temp)
    $freed+=($items | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $temp -Recurse -Force
    if (Test-Path -LiteralPath $temp) { throw 'Task Temp cleanup failed.' }
}
$mainChanges=@(Import-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') | Where-Object { !(Test-Path -LiteralPath $_.Path) -or (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 })
$cleanupMainChanges=@($preCleanupHashes | Where-Object { $current=if(Test-Path -LiteralPath $_.Path){(Get-FileHash -LiteralPath $_.Path).Hash}else{'MISSING'}; $current -ne $_.SHA256 })
[pscustomobject]@{ComparedInputs=$preCleanupHashes.Count;DifferencesDuringCleanup=$cleanupMainChanges.Count;UnrelatedChangesSincePreparation=$mainChanges.Count;ReclaimedBytes=$freed} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'cleanup-verification.json') -Encoding utf8
if($cleanupMainChanges.Count){$cleanupMainChanges | Export-Csv -LiteralPath (Join-Path $output 'changes-during-cleanup.csv') -NoTypeInformation -Encoding utf8}
Remove-Item -LiteralPath $lease -Force
@('Borrowed project source restored and verified.','No files inside the main UnityClient project were written by validation or cleanup.',('Main source differences since snapshot: '+$mainChanges.Count),('Bytes reclaimed: '+$freed),('Pre-existing Temp retained: '+$prep.HadTemp),'Shared Library retained: needed by SmallTextAudit, WindowFit, ShipyardAssembly and existing screenshot tools.','Task scripts, source snapshots, original borrowed source, logs and screenshots retained.') | Set-Content -LiteralPath (Join-Path $output 'cleanup.txt') -Encoding utf8
if ($mainChanges.Count) { $mainChanges | Export-Csv -LiteralPath (Join-Path $output 'concurrent-main-source-changes.csv') -NoTypeInformation -Encoding utf8 }
Write-Output ('Cleanup complete, bytes reclaimed '+$freed+', concurrent main differences '+$mainChanges.Count)
