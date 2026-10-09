param(
    [string]$Project = 'D:/Game/tmp/第七港口v2-build-project',
    [string]$Output = 'D:/Game/Artifacts/SequentialOptimization20261008/02-Stability/ProfileJournal',
    [string]$Unity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [string]$ExistingBuild,
    [switch]$BuildOnly,
    [switch]$RunOnly
)
$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditProject = [IO.Path]::GetFullPath($Project)
$auditOutput = [IO.Path]::GetFullPath($Output)
if (-not $auditProject.StartsWith((Join-Path $auditWorkspace 'tmp') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated project under workspace tmp.' }
if (-not $auditOutput.StartsWith($auditWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must remain inside the workspace.' }
if ($BuildOnly -and $RunOnly) { throw 'BuildOnly and RunOnly are mutually exclusive.' }
if ($ExistingBuild -and !$RunOnly) { throw 'ExistingBuild requires RunOnly.' }
New-Item -ItemType Directory -Path $auditOutput -Force | Out-Null
$auditExecutable = Join-Path $auditOutput 'build/ProfileJournalAudit.exe'
$auditSource = Join-Path $auditProject 'Assets/_Project/Presentation/Runtime/CoreGameplayDemoController.cs'
$auditManifestPath = Join-Path $auditOutput 'source-and-stop-manifest.json'

function Write-NewJson([string]$Path, $Value) {
    if (Test-Path -LiteralPath $Path) { throw "Refusing to replace evidence: $Path" }
    $Value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Backup-AuditFile([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return }
    $relative = [IO.Path]::GetRelativePath($auditProject, $Path)
    if ($relative.StartsWith('..')) { throw 'Backup source escaped the isolated project.' }
    $backup = Join-Path $auditOutput ('before/' + $relative)
    if (Test-Path -LiteralPath $backup) { throw "Backup already exists: $backup" }
    New-Item -ItemType Directory -Force -Path (Split-Path $backup) | Out-Null
    Copy-Item -LiteralPath $Path -Destination $backup
    return $backup
}
function Audit-Arguments([string[]]$Values) {
    # Windows command-line paths cannot contain quotes; quote every token so
    # spaces in a caller's output/project paths are not split by Start-Process.
    return (($Values | ForEach-Object { if ($_ -match '"') { throw 'Argument contains a quote.' }; '"' + $_ + '"' }) -join ' ')
}
function Export-AuditRegistry([string]$Name) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\ShatteredSailAudit\Optimization20261008', $false)
    try {
        $values = @()
        if ($null -ne $key) {
            foreach ($registryValueName in ($key.GetValueNames() | Sort-Object)) {
                $value = $key.GetValue($registryValueName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                $kind = $key.GetValueKind($registryValueName).ToString()
                if ($value -is [byte[]]) { $value = [Convert]::ToBase64String($value) }
                $values += [ordered]@{ name = $registryValueName; kind = $kind; value = $value }
            }
        }
        Write-NewJson (Join-Path $auditOutput ($Name + '.json')) ([ordered]@{ scope = 'Read-only entire audit-identity PlayerPrefs registry key; binary values are Base64'; key = 'HKCU/Software/ShatteredSailAudit/Optimization20261008'; values = $values })
    } finally { if ($null -ne $key) { $key.Dispose() } }
}

if ($ExistingBuild) {
    $auditExecutable = [IO.Path]::GetFullPath($ExistingBuild)
    if (!(Test-Path -LiteralPath $auditExecutable -PathType Leaf) -or
        !$auditExecutable.StartsWith($auditWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'ExistingBuild must be an existing audit executable inside the workspace.' }
    $existingRoot = Split-Path (Split-Path $auditExecutable)
    $existingManifest = Join-Path $existingRoot 'source-and-stop-manifest.json'
    $existingHashes = Join-Path $existingRoot 'build-binary-hashes.json'
    if (!(Test-Path -LiteralPath $existingManifest) -or !(Test-Path -LiteralPath $existingHashes)) { throw 'Existing build requires its original source and binary manifests.' }
    $sourceManifest = Get-Content -LiteralPath $existingManifest -Raw | ConvertFrom-Json
    if ($sourceManifest.identity -cne 'ShatteredSailAudit/Optimization20261008') { throw 'Existing build source manifest has the wrong audit identity.' }
    $binaryRecords = @(Get-Content -LiteralPath $existingHashes -Raw | ConvertFrom-Json)
    $verifiedBinaries = @($binaryRecords | ForEach-Object {
        $binaryPath = [IO.Path]::GetFullPath((Join-Path $existingRoot $_.path))
        if (!$binaryPath.StartsWith((Split-Path $auditExecutable) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Binary manifest path escaped its build directory.' }
        $actualHash = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash
        if ($actualHash -cne $_.sha256) { throw "Existing build hash mismatch: $binaryPath" }
        [ordered]@{ path = $binaryPath; sha256 = $actualHash }
    })
    if (@($verifiedBinaries | Where-Object { $_.path -ieq $auditExecutable }).Count -ne 1) { throw 'Binary manifest must identify the reused executable exactly once.' }
    Write-NewJson $auditManifestPath $sourceManifest
    Write-NewJson (Join-Path $auditOutput 'reused-build.json') ([ordered]@{
        executable = $auditExecutable; originalEvidenceRoot = $existingRoot;
        originalSourceManifest = $existingManifest; sourceManifestSha256 = (Get-FileHash -LiteralPath $existingManifest -Algorithm SHA256).Hash;
        originalBinaryManifest = $existingHashes; binaryManifestSha256 = (Get-FileHash -LiteralPath $existingHashes -Algorithm SHA256).Hash;
        verifiedBinaries = $verifiedBinaries; runnerSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;
        scope = 'Runs the original instrumented build in place; all recorded executable and DLL hashes verified. No rebuild or binary copy.'
    })
}


if (-not $RunOnly) {
    if (Test-Path -LiteralPath $auditExecutable) { throw 'Choose a fresh output directory.' }
    $journalSource = Join-Path $auditProject 'Assets/_Project/Presentation/Runtime/ProfileSaveJournal.cs'
    $controllerOriginal = [IO.File]::ReadAllText($auditSource)
    $journalOriginal = [IO.File]::ReadAllText($journalSource)
    $controllerBackup = Backup-AuditFile $auditSource
    $journalBackup = Backup-AuditFile $journalSource
    $start = $controllerOriginal.IndexOf('private void SaveProgress()', [StringComparison]::Ordinal)
    $end = $controllerOriginal.IndexOf('public static string CargoKey(', $start, [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -le $start) { throw 'Cannot identify SaveProgress method region.' }
    $method = $controllerOriginal.Substring($start, $end - $start)
    function Hook([string]$Point) {
        return @"

            // AUDIT-ONLY injection in an isolated source copy.
            if (!UnityEngine.Application.isEditor && UnityEngine.Application.companyName == "ShatteredSailAudit" &&
                UnityEngine.Application.productName == "Optimization20261008" &&
                System.Environment.GetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED") == "1")
                System.Type.GetType("ShatteredSail.ProfileJournalAudit.PlayerProfileJournalProbe, ShatteredSail.PlayerProfileJournalAudit", true)
                    .GetMethod("RecordStopAndWait", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { "$Point" });

"@
    }
    foreach ($anchor in @('RefreshCollectionRewards();','QueueSeaChartProgress();','PlayerPrefs.Save();')) {
        if ([regex]::Matches($method, [regex]::Escape($anchor)).Count -ne 1) { throw "Expected unique SaveProgress anchor: $anchor" }
    }
    $method = $method.Replace('RefreshCollectionRewards();', (Hook 'after_journal') + 'RefreshCollectionRewards();')
    $method = $method.Replace('QueueSeaChartProgress();', 'QueueSeaChartProgress();' + (Hook 'after_chart'))
    $method = $method.Replace('PlayerPrefs.Save();', 'PlayerPrefs.Save();' + (Hook 'after_flush'))
    $controllerPatched = $controllerOriginal.Substring(0,$start) + $method + $controllerOriginal.Substring($end)
    if ([regex]::Matches($journalOriginal, [regex]::Escape('RestoreEntry(entry);')).Count -ne 1) { throw 'Expected unique journal replay call.' }
    $journalPatched = $journalOriginal.Replace('RestoreEntry(entry);', 'RestoreEntry(entry);' + (Hook 'during_recovery'))
    $runtime = Join-Path $auditProject 'Assets/PlayerProfileJournalAudit'
    $editor = Join-Path $auditProject 'Assets/PlayerPerformanceAudit/Editor'
    New-Item -ItemType Directory -Force -Path $runtime,$editor | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayerProfileJournalProbe.cs') -Destination $runtime
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../PlayerPerformanceAudit/PlayerPerformanceAuditBuilder.cs') -Destination $editor
    '{"name":"ShatteredSail.PlayerProfileJournalAudit","references":["ShatteredSail.Presentation","ShatteredSail.Core","ShatteredSail.Content"],"autoReferenced":true}' | Set-Content (Join-Path $runtime 'ShatteredSail.PlayerProfileJournalAudit.asmdef') -Encoding utf8
    '{"name":"ShatteredSail.PlayerPerformanceAudit.Editor","references":[],"includePlatforms":["Editor"],"autoReferenced":true}' | Set-Content (Join-Path $editor 'ShatteredSail.PlayerPerformanceAudit.Editor.asmdef') -Encoding utf8
    [IO.File]::WriteAllText((Join-Path $auditOutput 'Controller.instrumented.cs.txt'),$controllerPatched,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $auditOutput 'Journal.instrumented.cs.txt'),$journalPatched,[Text.UTF8Encoding]::new($false))
    Write-NewJson $auditManifestPath ([ordered]@{ identity='ShatteredSailAudit/Optimization20261008'; project=$auditProject; sourceControllerSha256=(Get-FileHash $auditSource).Hash; sourceJournalSha256=(Get-FileHash $journalSource).Hash; probeSha256=(Get-FileHash (Join-Path $runtime 'PlayerProfileJournalProbe.cs')).Hash; points=@('after_journal','after_chart','after_flush','during_recovery'); purpose='Raw preferences and actual production recovery are recorded separately. Source hooks exist only in this diagnostic build.' })
    try {
        [IO.File]::WriteAllText($auditSource,$controllerPatched,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($journalSource,$journalPatched,[Text.UTF8Encoding]::new($false))
        $buildArgs = Audit-Arguments @('-batchmode','-quit','-projectPath',$auditProject,'-executeMethod','ShatteredSail.Editor.PlayerPerformanceAuditBuilder.BuildFromCommandLine','-playerAuditBuildOutput',$auditExecutable,'-logFile',(Join-Path $auditOutput 'build.log'))
        $buildProcess = Start-Process -FilePath $Unity -ArgumentList $buildArgs -WindowStyle Hidden -PassThru
        Write-Output ('JOURNAL_BUILD_PID=' + $buildProcess.Id)
        $buildProcess.WaitForExit()
        if ($buildProcess.ExitCode -ne 0 -or !(Test-Path $auditExecutable)) { throw 'Journal build failed; see build.log.' }
        Write-NewJson (Join-Path $auditOutput 'build-binary-hashes.json') (@(Get-ChildItem (Split-Path $auditExecutable) -Recurse -File | Where-Object { $_.Extension -in @('.exe','.dll') } | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($auditOutput,$_.FullName);sha256=(Get-FileHash $_.FullName).Hash} }))
    } finally {
        Copy-Item -LiteralPath $controllerBackup -Destination $auditSource
        Copy-Item -LiteralPath $journalBackup -Destination $journalSource
        if ((Get-FileHash $auditSource).Hash -ne (Get-FileHash $controllerBackup).Hash -or (Get-FileHash $journalSource).Hash -ne (Get-FileHash $journalBackup).Hash) { throw 'Restored source hashes differ.' }
    }
    if ($BuildOnly) { Write-Output ('JOURNAL_BUILT=' + $auditExecutable); return }
}
if (!(Test-Path $auditExecutable) -or !(Test-Path $auditManifestPath)) { throw 'Build and source manifest required.' }
$auditEvidenceRoot = $auditOutput
$stageProcesses = [Collections.Generic.List[object]]::new()


Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class JournalAuditChildProcess {
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool TerminateProcess(IntPtr process, uint code);
}
'@
function Wait-OwnedExit([Diagnostics.Process]$Child) {
    # HasExited/Get-Process can claim a native-exiting Unity process is gone
    # while its final thread still holds memory. Require the OS inventory to
    # remove the child before starting another worker.
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 15) {
        if ($null -eq (Get-CimInstance Win32_Process -Filter ("ProcessId = " + $Child.Id))) { return }
        Start-Sleep -Milliseconds 100
    }
    throw 'Audit-owned child did not disappear after bounded termination.'
}
function Stop-OwnedChild([Diagnostics.Process]$Child) {
    # Start-Process retains the handle, so a recycled PID cannot redirect this.
    if (![JournalAuditChildProcess]::TerminateProcess($Child.Handle, 99)) {
        if ($null -ne (Get-CimInstance Win32_Process -Filter ("ProcessId = " + $Child.Id))) {
            throw ('Cannot terminate owned audit child: ' + [Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }
    }
    return $true
}
function Read-OwnedStageReport([string]$Path, [int]$OwnedProcessId, [string]$Mode) {
    if (!(Test-Path -LiteralPath $Path)) { return $null }
    # The probe initially reserves an empty file and later writes whole JSON.
    # A partial concurrent write is not a completed report.
    try { $value = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop } catch { return $null }
    if ($null -eq $value) { return $null }
    if ($value.pid -ne $OwnedProcessId -or $value.mode -cne $Mode -or
        $value.company -cne 'ShatteredSailAudit' -or $value.product -cne 'Optimization20261008') { throw 'Report does not belong to the owned audit child, mode and identity.' }
    return $value
}
function Run-AuditStage([string]$Mode, [string]$Name, [bool]$Crash = $false, [string]$Point = 'none') {
    $path = Join-Path $auditOutput ($Name + '.json')
    if (Test-Path -LiteralPath $path) { throw "Stage evidence already exists: $path" }
    if ($Crash -and (Test-Path -LiteralPath (Join-Path $auditOutput ($Name + '.stop')))) { throw 'Stop marker already exists; use a fresh audit root.' }
    $values = @('-batchmode','-nographics','-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-profileJournalMode',$Mode,'-profileJournalRoot',$auditOutput,'-profileJournalReport',$path,'-profileJournalStopPoint',$Point,'-logFile',(Join-Path $auditOutput ($Name + '.log')))
    $process = Start-Process -FilePath $auditExecutable -ArgumentList (Audit-Arguments $values) -WindowStyle Hidden -PassThru
    $entry = [ordered]@{ mode = $Mode; report = $path; pid = $process.Id; startedUtc = [DateTime]::UtcNow.ToString('O'); killedAtStop = $false; killedAfterCompletedReport = $false; killedDuringCleanup = $false; completedReportUtc = $null; termination = $null; exitCode = $null }
    $stageProcesses.Add($entry)
    try {
        if ($Crash) {
            $marker = Join-Path $auditOutput ($Name + '.stop')
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while (!(Test-Path -LiteralPath $marker) -and !$process.HasExited -and $timer.Elapsed.TotalSeconds -lt 60) { Start-Sleep -Milliseconds 100 }
            if (!(Test-Path -LiteralPath $marker)) { throw 'Crash process did not reach the declared save stop.' }
            $markerText = [IO.File]::ReadAllText($marker)
            if ($markerText -notmatch ('(?m)^pid=' + $process.Id + '\r?$')) { throw 'Stop marker PID is not the child process started by this runner.' }
            $result = Read-OwnedStageReport $path $process.Id $Mode
            if ($null -eq $result -or $result.status -cne 'waiting_for_runner_kill') { throw 'Crash marker has no matching complete stop report.' }
            Export-AuditRegistry ($Name + '-registry-at-stop')
            Stop-OwnedChild $process | Out-Null
            $entry.killedAtStop = $true
            $entry.termination = 'runner_kill_at_declared_save_stop'
            Wait-OwnedExit $process
        } else {
            $timer = [Diagnostics.Stopwatch]::StartNew()
            $result = $null
            do {
                $result = Read-OwnedStageReport $path $process.Id $Mode
                if ($null -ne $result -and $result.status -ceq 'failed') { throw "Audit child reported failure: $Name : $($result.error)" }
                if ($null -ne $result -and $result.status -ceq 'passed' -and ![string]::IsNullOrEmpty($result.completedUtc)) { break }
                if ($process.HasExited) { throw "Audit child exited without a completed passing report: $Name" }
                Start-Sleep -Milliseconds 100
            } while ($timer.Elapsed.TotalSeconds -lt 60)
            if ($null -eq $result -or $result.status -cne 'passed' -or [string]::IsNullOrEmpty($result.completedUtc)) { throw 'Audit child exceeded its 60-second report deadline.' }
            $entry.completedReportUtc = $result.completedUtc
            # The probe holds after its completed report. Terminate the exact
            # retained child handle; never enter Unity/Mono shutdown savers.
            Stop-OwnedChild $process | Out-Null
            $entry.killedAfterCompletedReport = $true
            $entry.termination = 'runner_termination_after_verified_completed_report'
            Wait-OwnedExit $process
        }
        $entry.exitCode = $process.ExitCode
        if (!(Test-Path -LiteralPath $path)) { throw "Audit report missing: $Name" }
        $result = Read-OwnedStageReport $path $process.Id $Mode
        $wanted = if ($Crash) { 'waiting_for_runner_kill' } else { 'passed' }
        if ($result.status -ne $wanted) { throw "Unexpected stage status $($result.status): $Name" }
        return $result
    } finally {
        if ($null -ne (Get-CimInstance Win32_Process -Filter ("ProcessId = " + $process.Id))) { Stop-OwnedChild $process | Out-Null; $entry.killedDuringCleanup = $true; $entry.termination = 'runner_kill_during_failed_stage_cleanup'; Wait-OwnedExit $process }
        $entry.exitCode = $process.ExitCode
        $process.Dispose()
    }
}


$fields = @('gold','legacyGold','water','food','wood','iron','cloth','powder','cannonballs','beer','medicine','cargoGold','fame','chartJson','treasureJson','chartCount','chartVisits','fragments','pirateExperience','treasureMaps')
$cases = [Collections.Generic.List[object]]::new()
try {
    foreach ($point in @('after_journal','after_chart','after_flush','during_recovery')) {
        $auditOutput = Join-Path $auditEvidenceRoot $point
        if (Test-Path $auditOutput) { throw "Case directory already exists: $auditOutput" }
        New-Item -ItemType Directory -Path $auditOutput | Out-Null
        $seed = Run-AuditStage 'seed' '01-seed'
        $seedDisk = Run-AuditStage 'inspect' '02-inspect-seed'
        $writePoint = if ($point -eq 'during_recovery') { 'after_chart' } else { $point }
        $crash = Run-AuditStage 'crash' '03-crash' $true $writePoint
        # Recovery returns to the exact pre-SaveProgress boundary. Keep the
        # seed report separately: normal scene startup may commit its own state.
        $beforeGroup = $crash.beforeCommitDisk
        $raw = Run-AuditStage 'inspect' '04-raw-after-kill'
        if ($point -eq 'during_recovery') { $recoveryCrash = Run-AuditStage 'recover_crash' '05-recovery-crash' $true 'during_recovery' }
        $recovered = Run-AuditStage 'recover' '06-recovered'
        $recoveredAgain = Run-AuditStage 'recover' '07-recovered-again'
        $complete = Run-AuditStage 'complete' '08-complete'
        $completeDisk = Run-AuditStage 'recover' '09-complete-restart'
        $comparison = @($fields | ForEach-Object {
            $field = $_
            [ordered]@{field=$field;before=$beforeGroup.$field;pending=$crash.expectedSession.$field;rawAfterKill=$raw.initialDisk.$field;afterRecovery=$recovered.afterRecovery.$field;afterSecondRecovery=$recoveredAgain.afterRecovery.$field;afterComplete=$completeDisk.afterRecovery.$field;recoveredOld=($recovered.afterRecovery.$field -ceq $beforeGroup.$field);recoveredAgainOld=($recoveredAgain.afterRecovery.$field -ceq $beforeGroup.$field);completeMatches=($completeDisk.afterRecovery.$field -ceq $complete.expectedSession.$field)}
        })
        $comparison += [ordered]@{field='all_100_typed_protected_keys';before=$beforeGroup.protectedValues;afterRecovery=$recovered.afterRecovery.protectedValues;afterSecondRecovery=$recoveredAgain.afterRecovery.protectedValues;afterComplete=$completeDisk.afterRecovery.protectedValues;recoveredOld=($recovered.afterRecovery.protectedValues -ceq $beforeGroup.protectedValues);recoveredAgainOld=($recoveredAgain.afterRecovery.protectedValues -ceq $beforeGroup.protectedValues);completeMatches=($completeDisk.afterRecovery.protectedValues -ceq $complete.afterComplete.protectedValues)}
        $pass = @($comparison | Where-Object { !$_.recoveredOld -or !$_.recoveredAgainOld -or !$_.completeMatches }).Count -eq 0
        $case = [ordered]@{point=$point;passed=$pass;comparison=$comparison;notice=$recovered.recoveryNotice}
        $cases.Add($case)
        Write-NewJson (Join-Path $auditOutput 'result.json') $case
        if (!$pass) { throw "Journal recovery mismatch: $point" }
        Write-Output ('JOURNAL_CASE_PASSED=' + $point)
    }
} finally {
    Write-NewJson (Join-Path $auditEvidenceRoot 'processes.json') $stageProcesses
    Write-NewJson (Join-Path $auditEvidenceRoot 'summary.json') ([ordered]@{passed=@($cases | Where-Object passed).Count;caseCount=$cases.Count;expectedCaseCount=4;cases=$cases;limits='Controlled Windows process interruption at declared same-source save/recovery points. Production recovery checked before scene Awake, then independently again. Explicit fixture, not a gameplay journey; no power-loss or general transaction guarantee.'})
}
