param(
    [string]$Project = 'D:/Game/tmp/第七港口v2-build-project',
    [string]$Output = 'D:/Game/Artifacts/SequentialOptimization20261008/02-Stability/ProfileCommit',
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
$auditExecutable = Join-Path $auditOutput 'build/ProfileCommitAudit.exe'
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
    if (Test-Path -LiteralPath $auditExecutable) { throw 'Choose a fresh output directory; existing builds are preserved.' }
    if (Test-Path -LiteralPath $auditManifestPath) { throw 'This output already contains a source manifest.' }
    $auditOriginal = [IO.File]::ReadAllText($auditSource)
    $auditBefore = Backup-AuditFile $auditSource
    $methodStart = $auditOriginal.IndexOf('private void SaveProgress()', [StringComparison]::Ordinal)
    if ($methodStart -lt 0) { throw 'Cannot identify SaveProgress method.' }
    $methodEnd = $auditOriginal.IndexOf('public static string CargoKey(', $methodStart, [StringComparison]::Ordinal)
    if ($methodStart -lt 0 -or $methodEnd -le $methodStart) { throw 'Cannot identify exact SaveProgress method bounds.' }
    $method = $auditOriginal.Substring($methodStart, $methodEnd - $methodStart)
    $chartCalls = [regex]::Matches($method, '(?m)^\s*(SaveSeaChartProgress|QueueSeaChartProgress)\(\);\s*$')
    if ($chartCalls.Count -ne 1) { throw 'Expected exactly one standalone SaveSeaChartProgress or QueueSeaChartProgress call inside SaveProgress.' }
    $chartOperation = $chartCalls[0].Groups[1].Value
    $anchor = $chartOperation + '();'
    $hook = @'

            // AUDIT ONLY: isolated build; no reference from Presentation to the probe assembly.
            if (!Application.isEditor && Application.companyName == "ShatteredSailAudit" && Application.productName == "Optimization20261008" &&
                System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-profileCommitCrashAfterSeaChart") >= 0 &&
                System.Environment.GetEnvironmentVariable("SHATTEREDSAIL_PROFILE_AUDIT_ARMED") == "1")
            {
                System.Type.GetType("ShatteredSail.ProfileAudit.PlayerProfileCommitProbe, ShatteredSail.PlayerProfileAudit", true)
                    .GetMethod("RecordStopAndWait", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
            }
'@
    $hookOffset = $methodStart + $method.IndexOf($anchor, [StringComparison]::Ordinal) + $anchor.Length
    $auditPatched = $auditOriginal.Insert($hookOffset, $hook)
    $auditRuntime = Join-Path $auditProject 'Assets/PlayerProfileAudit'
    $auditEditor = Join-Path $auditProject 'Assets/PlayerPerformanceAudit/Editor'
    New-Item -ItemType Directory -Path $auditRuntime,$auditEditor -Force | Out-Null
    $probeDestination = Join-Path $auditRuntime 'PlayerProfileCommitProbe.cs'
    $builderDestination = Join-Path $auditEditor 'PlayerPerformanceAuditBuilder.cs'
    Backup-AuditFile $probeDestination | Out-Null
    Backup-AuditFile $builderDestination | Out-Null
    $runtimeAsmdef = Join-Path $auditRuntime 'ShatteredSail.PlayerProfileAudit.asmdef'
    $editorAsmdef = Join-Path $auditEditor 'ShatteredSail.PlayerPerformanceAudit.Editor.asmdef'
    Backup-AuditFile $runtimeAsmdef | Out-Null
    Backup-AuditFile $editorAsmdef | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayerProfileCommitProbe.cs') -Destination $probeDestination
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../PlayerPerformanceAudit/PlayerPerformanceAuditBuilder.cs') -Destination $builderDestination
    '{"name":"ShatteredSail.PlayerProfileAudit","references":["ShatteredSail.Presentation","ShatteredSail.Core","ShatteredSail.Content"],"autoReferenced":true}' | Set-Content -LiteralPath $runtimeAsmdef -Encoding utf8
    '{"name":"ShatteredSail.PlayerPerformanceAudit.Editor","references":[],"includePlatforms":["Editor"],"autoReferenced":true}' | Set-Content -LiteralPath $editorAsmdef -Encoding utf8
    $patchedEvidence = Join-Path $auditOutput 'CoreGameplayDemoController.instrumented.cs.txt'
    [IO.File]::WriteAllText($patchedEvidence, $auditPatched, [Text.UTF8Encoding]::new($false))
    $sourceFiles = @('Assets/_Project/Presentation/Runtime/CoreGameplayDemoController.cs','Assets/_Project/Presentation/Runtime/CoreGameplaySeaChartPersistence.cs','Assets/_Project/Presentation/Runtime/CoreGameplayTreasureVoyagePresentation.cs','Assets/_Project/Presentation/Runtime/CoreGameplayCollectionRewards.cs','Assets/_Project/Presentation/Runtime/MainMenuController.cs','Assets/_Project/Presentation/Runtime/ProfileGoldPersistence.cs','Assets/_Project/Core/Runtime/CoreSeaChartProgress.cs','Assets/_Project/Core/Runtime/CoreSeaChartVoyage.cs','Assets/_Project/Core/Runtime/CoreTreasureHunt.cs')
    $hashes = @($sourceFiles | ForEach-Object { [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $auditProject $_) -Algorithm SHA256).Hash } })
    Write-NewJson $auditManifestPath ([ordered]@{ identity = 'ShatteredSailAudit/Optimization20261008'; sourceProject = $auditProject; originals = $hashes; backup = $auditBefore; instrumentedSha256 = (Get-FileHash -LiteralPath $patchedEvidence -Algorithm SHA256).Hash; probeSha256 = (Get-FileHash -LiteralPath $probeDestination -Algorithm SHA256).Hash; chartOperation = $chartOperation; stop = ('SaveProgress after ' + $chartOperation + ' returns, before SaveGameCalendar and ProfileGoldPersistence.Save'); hook = $hook; control = 'same instrumented build without crash gate completes ordinary SaveProgress' })
    try {
        [IO.File]::WriteAllText($auditSource, $auditPatched, [Text.UTF8Encoding]::new($false))
        $auditBuildArguments = Audit-Arguments @('-batchmode','-quit','-projectPath',$auditProject,'-executeMethod','ShatteredSail.Editor.PlayerPerformanceAuditBuilder.BuildFromCommandLine','-playerAuditBuildOutput',$auditExecutable,'-logFile',(Join-Path $auditOutput 'build.log'))
        $auditBuildProcess = Start-Process -FilePath $Unity -ArgumentList $auditBuildArguments -WindowStyle Hidden -PassThru
        Write-Output ('PROFILE_AUDIT_BUILD_PID=' + $auditBuildProcess.Id)
        $auditBuildProcess.WaitForExit()
        if ($auditBuildProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $auditExecutable)) { throw 'Profile audit build failed; inspect build.log.' }
        Write-NewJson (Join-Path $auditOutput 'build-binary-hashes.json') (@(Get-ChildItem -LiteralPath (Split-Path $auditExecutable) -Recurse -File | Where-Object { $_.Extension -in @('.exe','.dll') } | ForEach-Object { [ordered]@{ path = [IO.Path]::GetRelativePath($auditOutput, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }))
    } finally {
        Copy-Item -LiteralPath $auditBefore -Destination $auditSource
        if ((Get-FileHash -LiteralPath $auditSource -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $auditBefore -Algorithm SHA256).Hash) { throw 'Isolated source restoration hash mismatch.' }
    }
    if ($BuildOnly) { Write-Output ('PROFILE_AUDIT_BUILT=' + $auditExecutable); return }
}
if (!(Test-Path -LiteralPath $auditExecutable) -or !(Test-Path -LiteralPath $auditManifestPath)) { throw 'An instrumented build and source manifest are required.' }
$runningManifest = Get-Content -LiteralPath $auditManifestPath -Raw | ConvertFrom-Json
$auditStopOperation = $runningManifest.chartOperation
if ([string]::IsNullOrEmpty($auditStopOperation) -and $runningManifest.stop -match 'after SaveSeaChartProgress returns') { $auditStopOperation = 'SaveSeaChartProgress' }
if ($auditStopOperation -cnotin @('SaveSeaChartProgress','QueueSeaChartProgress')) { throw 'Source manifest has no recognized chart stop operation.' }
$stageProcesses = [Collections.Generic.List[object]]::new()
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
function Run-AuditStage([string]$Mode, [string]$Name, [bool]$Crash = $false) {
    $path = Join-Path $auditOutput ($Name + '.json')
    if (Test-Path -LiteralPath $path) { throw "Stage evidence already exists: $path" }
    if ($Crash -and (Test-Path -LiteralPath (Join-Path $auditOutput 'crash-stop.txt'))) { throw 'Stop marker already exists; use a fresh audit root.' }
    $values = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-profileCommitMode',$Mode,'-profileCommitRoot',$auditOutput,'-profileCommitReport',$path,'-profileCommitStopMethod',$auditStopOperation,'-logFile',(Join-Path $auditOutput ($Name + '.log')))
    if ($Crash) { $values += '-profileCommitCrashAfterSeaChart' }
    $process = Start-Process -FilePath $auditExecutable -ArgumentList (Audit-Arguments $values) -WindowStyle Hidden -PassThru
    $entry = [ordered]@{ mode = $Mode; report = $path; pid = $process.Id; startedUtc = [DateTime]::UtcNow.ToString('O'); killedAtStop = $false; killedAfterCompletedReport = $false; killedDuringCleanup = $false; completedReportUtc = $null; termination = $null; exitCode = $null }
    $stageProcesses.Add($entry)
    try {
        if ($Crash) {
            $marker = Join-Path $auditOutput 'crash-stop.txt'
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while (!(Test-Path -LiteralPath $marker) -and !$process.HasExited -and $timer.Elapsed.TotalSeconds -lt 60) { Start-Sleep -Milliseconds 100 }
            if (!(Test-Path -LiteralPath $marker)) { throw 'Crash process did not reach the declared save stop.' }
            $markerText = [IO.File]::ReadAllText($marker)
            if ($markerText -notmatch ('(?m)^pid=' + $process.Id + '\r?$')) { throw 'Stop marker PID is not the child process started by this runner.' }
            $result = Read-OwnedStageReport $path $process.Id $Mode
            if ($null -eq $result -or $result.status -cne 'waiting_for_runner_kill') { throw 'Crash marker has no matching complete stop report.' }
            Export-AuditRegistry 'registry-at-stop'
            Stop-Process -Id $process.Id -ErrorAction Stop
            $entry.killedAtStop = $true
            $entry.termination = 'runner_kill_at_declared_save_stop'
            $process.WaitForExit()
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
            # On this Unity Mono build Environment.Exit can hang after Save and
            # WriteReport. A verified finished stage may be audit-terminated;
            # this is explicitly different from a clean Player shutdown.
            if (!$process.WaitForExit(1000)) {
                Stop-Process -Id $process.Id -ErrorAction Stop
                $entry.killedAfterCompletedReport = $true
                $entry.termination = 'runner_kill_after_verified_completed_report_EnvironmentExit_did_not_finish'
                $process.WaitForExit()
            } else {
                $entry.termination = 'process_exited'
                if ($process.ExitCode -ne 0) { throw "Audit child failed after report: $Name" }
            }
        }
        $entry.exitCode = $process.ExitCode
        if (!(Test-Path -LiteralPath $path)) { throw "Audit report missing: $Name" }
        $result = Read-OwnedStageReport $path $process.Id $Mode
        $wanted = if ($Crash) { 'waiting_for_runner_kill' } else { 'passed' }
        if ($result.status -ne $wanted) { throw "Unexpected stage status $($result.status): $Name" }
        return $result
    } finally {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue; $entry.killedDuringCleanup = $true; $entry.termination = 'runner_kill_during_failed_stage_cleanup'; $process.WaitForExit() }
        $entry.exitCode = $process.ExitCode
        $process.Dispose()
    }
}

try {
    Export-AuditRegistry 'registry-before-seed'
    $seed = Run-AuditStage 'seed' '01-seed'
    $seedDisk = Run-AuditStage 'inspect' '02-inspect-seed'
    Export-AuditRegistry 'registry-seed'
    $crash = Run-AuditStage 'crash' '03-crash' $true
    Export-AuditRegistry 'registry-after-kill'
    $crashDisk = Run-AuditStage 'inspect' '04-inspect-crash'
    $controlSeed = Run-AuditStage 'seed' '05-control-seed'
    $complete = Run-AuditStage 'complete' '06-complete'
    $completeDisk = Run-AuditStage 'inspect' '07-inspect-complete'
    Export-AuditRegistry 'registry-complete'
    $fields = @('gold','legacyGold','water','food','wood','iron','cloth','powder','cannonballs','beer','medicine','cargoGold','fame','chartJson','treasureJson','chartCount','chartVisits','fragments','pirateExperience','treasureMaps')
    $comparison = @($fields | ForEach-Object { $field = $_; [ordered]@{ field = $field; before = $seedDisk.initialDisk.$field; pending = $crash.expectedSession.$field; afterCrash = $crashDisk.initialDisk.$field; afterComplete = $completeDisk.initialDisk.$field; crashIsOld = ($crashDisk.initialDisk.$field -ceq $seedDisk.initialDisk.$field); crashIsPending = ($crashDisk.initialDisk.$field -ceq $crash.expectedSession.$field); completeIsPending = ($completeDisk.initialDisk.$field -ceq $complete.expectedSession.$field) } })
    $changed = @($comparison | Where-Object { $_.before -cne $_.pending })
    $old = @($changed | Where-Object { $_.crashIsOld }).Count
    $new = @($changed | Where-Object { $_.crashIsPending }).Count
    $controlPassed = @($comparison | Where-Object { !$_.completeIsPending }).Count -eq 0
    $classification = if ($old -gt 0 -and $new -gt 0) { 'mixed_group_reproduced' } elseif ($old -eq $changed.Count) { 'all_observed_fields_old' } elseif ($new -eq $changed.Count) { 'all_observed_fields_pending' } else { 'other_inconsistent_result' }
    Write-NewJson (Join-Path $auditOutput 'result.json') ([ordered]@{ classification = $classification; completeControlPassed = $controlPassed; chartStopOperation = $auditStopOperation; stop = $runningManifest.stop; observedChangedFieldCount = $changed.Count; oldFieldCountAfterCrash = $old; pendingFieldCountAfterCrash = $new; comparison = $comparison; limitation = 'One gated interruption after the declared chart persistence operation in the same-source SaveProgress. The original SaveSeaChartProgress flushes; QueueSeaChartProgress only stages values. Registry files capture the entire audit identity; classification covers listed chart/treasure/gold/cargo fields. Completed stages may be terminated by the runner after a matching PID/identity passed report and one-second exit grace; see processes.json. They are not claimed to be clean shutdowns. No claim about all crash points or gameplay transactions.' })
    if (!$controlPassed) { throw 'Complete-save restart control did not match expected fields.' }
    Write-Output ('PROFILE_COMMIT_RESULT=' + $classification)
} finally {
    Write-NewJson (Join-Path $auditOutput 'processes.json') $stageProcesses
}
