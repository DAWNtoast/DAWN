param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Capture','Test','Status','Cleanup')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run = 'run1',
    [ValidateSet('validation','battle-validation')][string]$EvidenceSet = 'validation',
    [string]$TestFilter = 'ShatteredSail.Presentation.Tests.VoyageHeaderPrefabTests'
)
$ErrorActionPreference = 'Stop'
$workspace = 'D:\Game'
$main = Join-Path $workspace 'UnityClient'
$scratch = Join-Path $workspace 'tmp\shipyard-assembly-20261017'
$output = Join-Path $workspace ('outputs\header-wood-20261026\' + $EvidenceSet)
$journal = Join-Path $output 'scratch-journal.json'
$lease = Join-Path $scratch 'HeaderWood20261026.lock'
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
if ($Stage -eq 'Prepare') {
    if (Test-Path -LiteralPath $lease) { throw 'This task already owns the borrowed project; preserve journal.' }
    if (Test-Path -LiteralPath $journal) { throw 'Existing task journal must be preserved.' }
    if (!(Test-Path -LiteralPath (Join-Path $scratch 'Library'))) { throw 'Expected existing shared warm Library.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    [IO.File]::WriteAllText($lease,$output)
    Save-Journal
    [pscustomobject]@{Project=$scratch;HadTemp=(Test-Path -LiteralPath (Join-Path $scratch 'Temp'));StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'preparation.json') -Encoding utf8
    $inputs=[Collections.Generic.List[object]]::new()
    foreach ($part in @('Assets','Packages')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $main $part) -File -Recurse) {
            if ($file.FullName -like '*\UIPreviewsForMe*') { continue }
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked input: '+$file.FullName) }
            $relative=$file.FullName.Substring($main.Length+1)
            $target=Join-Path $scratch $relative
            Assert-Safe $target
            $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $inputs.Add([pscustomobject]@{Path=$file.FullName;SHA256=$hash;Length=$file.Length})
            if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash) { continue }
            Record-Target $target
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    # Quarantine stale scratch-only C# helpers outside Assets; restore them in Cleanup.
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $scratch 'Assets') -Filter '*.cs' -File -Recurse) {
        if ($file.FullName -like '*\UIPreviewsForMe*') { continue }
        $relative=$file.FullName.Substring($scratch.Length+1)
        if (Test-Path -LiteralPath (Join-Path $main $relative)) { continue }
        Record-Target $file.FullName
        Remove-Item -LiteralPath $file.FullName -Force
    }
    $inputs | Export-Csv -LiteralPath (Join-Path $output 'main-source-hashes.csv') -NoTypeInformation -Encoding utf8
    $target=Join-Path $scratch 'Assets\_Project\Editor\HeaderWoodCapture20261026.cs'
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
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform','EditMode','-testFilter',$TestFilter,'-testResults',(Join-Path $testOutput 'EditMode.xml'),'-logFile',(Join-Path $testOutput 'unity.log'))
    $process=Start-Process -FilePath $unity -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Project=$scratch;Output=$testOutput;Arguments=$arguments;StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $testOutput 'launch.json') -Encoding utf8
    Write-Output ('Started EditMode PID '+$process.Id+'; '+$testOutput)
    exit
}
if ($Stage -eq 'Capture') {
    $runOutput=Join-Path $output $Run
    if (Test-Path -LiteralPath $runOutput) { throw 'Choose a fresh run name.' }
    New-Item -ItemType Directory -Path $runOutput | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $scratch 'Assets\_Project\Editor\HeaderWoodCapture20261026.cs') -Force
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-executeMethod','ShatteredSail.Editor.HeaderWoodCapture20261026.CaptureFromCommandLine','-headerWoodOutput',$runOutput,'-logFile',(Join-Path $runOutput 'unity.log'),'-quit')
    if ($EvidenceSet -eq 'battle-validation') { $arguments += '-headerWoodBattle' }
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
@('Borrowed project source restored and verified.','No files inside the main UnityClient project were written by validation or cleanup.',('Main source differences since snapshot: '+$mainChanges.Count),('Bytes reclaimed: '+$freed),('Pre-existing Temp retained: '+$prep.HadTemp),'Shared Library retained: needed by SmallTextAudit, WindowFit, ShipyardAssembly and existing screenshot tools.','Task scripts, source snapshots, original borrowed source, logs and screenshots retained.') | Set-Content -LiteralPath (Join-Path $output 'cleanup.txt') -Encoding utf8
if ($mainChanges.Count) { $mainChanges | Export-Csv -LiteralPath (Join-Path $output 'concurrent-main-source-changes.csv') -NoTypeInformation -Encoding utf8 }
Write-Output ('Cleanup complete, bytes reclaimed '+$freed+', concurrent main differences '+$mainChanges.Count)
