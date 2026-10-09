from pathlib import Path

root = Path(__file__).parent
s = (root.parent / 'PlayerProfileAudit/Run.ps1').read_text(encoding='utf-8-sig')
prefix = s[:s.index('if (-not $RunOnly) {')]
prefix = prefix.replace('02-Stability/ProfileCommit', '02-Stability/ProfileJournal')
prefix = prefix.replace('build/ProfileCommitAudit.exe', 'build/ProfileJournalAudit.exe')
function_start = s.index('function Read-OwnedStageReport')
function_end = s.index('\ntry {\n    Export-AuditRegistry', function_start)
functions = s[function_start:function_end]
functions = functions.replace("$auditStopOperation", "'QueueSeaChartProgress'")
functions = functions.replace('profileCommit', 'profileJournal')
functions = functions.replace("[bool]$Crash = $false)", "[bool]$Crash = $false, [string]$Point = 'none')")
functions = functions.replace("'crash-stop.txt'", "($Name + '.stop')")
functions = functions.replace("'-profileJournalStopMethod','QueueSeaChartProgress'", "'-profileJournalStopPoint',$Point")
functions = functions.replace("    if ($Crash) { $values += '-profileJournalCrashAfterSeaChart' }\n", '')
functions = functions.replace("Export-AuditRegistry 'registry-at-stop'", "Export-AuditRegistry ($Name + '-registry-at-stop')")
functions = functions.replace("$values = @('-screen-fullscreen'", "$values = @('-batchmode','-nographics','-screen-fullscreen'")
functions = functions.replace('Stop-Process -Id $process.Id -ErrorAction Stop', 'Stop-OwnedChild $process | Out-Null')
functions = functions.replace('Stop-Process -Id $process.Id -ErrorAction SilentlyContinue', 'Stop-OwnedChild $process | Out-Null')
functions = functions.replace('$process.WaitForExit()', 'Wait-OwnedExit $process')
start = functions.index('            # On this Unity Mono build')
end = functions.index('\n        }\n        $entry.exitCode', start)
functions = functions[:start] + '''            # The probe holds after its completed report. Terminate the exact
            # retained child handle; never enter Unity/Mono shutdown savers.
            Stop-OwnedChild $process | Out-Null
            $entry.killedAfterCompletedReport = $true
            $entry.termination = 'runner_termination_after_verified_completed_report'
            Wait-OwnedExit $process''' + functions[end:]
functions = functions.replace('if (!$process.HasExited) { Stop-OwnedChild', 'if ($null -ne (Get-CimInstance Win32_Process -Filter ("ProcessId = " + $process.Id))) { Stop-OwnedChild')
functions = r'''
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
''' + functions

build = r'''
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
'''

run = r'''
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
'''
(root / 'Run.ps1').write_text(prefix + build + '\n' + functions + '\n' + run, encoding='utf-8')
print('Prepared journal runner; validate anchors, syntax, compile and execution before accepting.')
