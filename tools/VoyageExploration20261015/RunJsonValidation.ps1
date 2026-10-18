param(
    [string]$Output = 'D:/Game/Artifacts/VoyageExploration20261015/UnityJson',
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [ValidateRange(30, 7200)][int]$TimeoutSeconds = 900,
    [ValidateRange(1, 60)][int]$HeartbeatSeconds = 15
)
$ErrorActionPreference = 'Stop'
$explorationWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$explorationProject = Join-Path $explorationWorkspace 'tmp/voyage-event-expansion-20261014'
# Keep each run's logs/results. A previous successful XML must never satisfy a new run.
$explorationRunName = 'run-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$explorationOutput = Join-Path ([IO.Path]::GetFullPath($Output)) $explorationRunName
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity editor not found: $UnityEditor" }
# An unclean previous exit can leave an unlocked file. Recover only this marked
# validation directory, after both the process command line and OS lock checks.
$explorationLock = Join-Path $explorationProject 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $explorationLock) {
    $explorationMarker = Join-Path $explorationProject 'VOYAGE_EVENT_VALIDATION_20261014.txt'
    if (-not (Test-Path -LiteralPath $explorationMarker)) { throw 'Cannot recover an unowned project lock.' }
    $explorationNormalizedProject = $explorationProject.Replace('\', '/').ToLowerInvariant()
    $explorationEditors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('\', '/').ToLowerInvariant().Contains($explorationNormalizedProject)
    })
    if ($explorationEditors.Count -gt 0) { throw 'The isolated project has a live Unity process; leave its lock intact.' }
    $explorationLockStream = $null
    try {
        # DeleteOnClose removes only the file successfully opened exclusively.
        # A live editor's lock fails this open, and is never removed.
        $explorationLockStream = [IO.FileStream]::new($explorationLock, [IO.FileMode]::Open,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None, 4096, [IO.FileOptions]::DeleteOnClose)
    } catch { throw "The isolated project's lock cannot be opened exclusively; leave it intact. $($_.Exception.Message)" }
    finally { if ($null -ne $explorationLockStream) { $explorationLockStream.Dispose() } }
    Write-Output "Recovered an unlocked stale lock in this owned validation project: $explorationLock"
}
# The existing preparation checks its ownership marker and Unity lock, and
# copies only Core, tests, package references and isolated project settings.
& (Join-Path $explorationWorkspace 'tools/VoyageEventExpansion20261014/RunUnityValidation.ps1') -Output $explorationOutput -PrepareOnly
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ExplorationJsonTests.cs') -Destination (Join-Path $explorationProject 'Assets/Tests/EditMode/ExplorationJsonTests.cs') -Force
$explorationResult = Join-Path $explorationOutput 'results.xml'
$explorationLog = Join-Path $explorationOutput 'editor.log'
$explorationRunnerLog = Join-Path $explorationOutput 'runner.log'
$explorationUpmLog = Join-Path $env:LOCALAPPDATA 'Unity/Editor/upm.log'
# UPM logs rotate globally, so retain the previous startup evidence before launching.
if (Test-Path -LiteralPath $explorationUpmLog) {
    Copy-Item -LiteralPath $explorationUpmLog -Destination (Join-Path $explorationOutput 'upm-before.log')
}
$explorationExpected = @(
    'ExplorationJsonTests.UnityJson_MissingExplorationFieldsDefaultsToEmptyState',
    'ExplorationJsonTests.UnityJson_RetainsExplorationWindowsAndCluesAcrossCaptureAndRestore'
)
$explorationWatch = [Diagnostics.Stopwatch]::StartNew()
$explorationProcess = $null
$explorationStatus = 'Failed'
$explorationFailure = $null
$explorationExitCode = $null
$explorationPhase = 'Starting editor'
function Write-ExplorationHeartbeat {
    param([string]$Message)
    $explorationLine = [DateTimeOffset]::Now.ToString('o') + ' ' + $Message
    Add-Content -LiteralPath $explorationRunnerLog -Value $explorationLine -Encoding utf8
    Write-Output $explorationLine
}
function Get-ExplorationPhase {
    if (-not (Test-Path -LiteralPath $explorationLog)) { return 'Starting editor' }
    $explorationText = Get-Content -LiteralPath $explorationLog -Raw
    if ($explorationText -match 'Test run completed') { return 'Tests complete / editor shutdown' }
    if ($explorationText -match 'Saving results to:') { return 'Writing test results' }
    if ($explorationText -match 'Running tests for ExecutionSettings') { return 'Running EditMode tests' }
    if ($explorationText -match '\[Project\] Loading completed') { return 'Project loaded / starting tests' }
    if ($explorationText -match 'DisplayProgressbar: Compiling Scripts|\[ScriptCompilation\]') { return 'Compiling / reloading scripts' }
    if ($explorationText -match 'Application.AssetDatabase Initial Refresh Start') { return 'Initial refresh / resolving cached packages' }
    if ($explorationText -match '\[Package Manager\] Connected') { return 'Initializing editor and package manager' }
    return 'Starting editor / licensing'
}
$explorationArgs = @('-batchmode', '-nographics', '-projectPath', ('"' + $explorationProject + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testFilter', 'ExplorationJsonTests',
    '-testResults', ('"' + $explorationResult + '"'), '-logFile', ('"' + $explorationLog + '"'))
try {
    Write-ExplorationHeartbeat "Output: $explorationOutput; total timeout=${TimeoutSeconds}s. Log silence alone is not a hang."
    $explorationProcess = Start-Process -FilePath $UnityEditor -ArgumentList $explorationArgs -WindowStyle Hidden -PassThru
    # Retain the process handle before exit, so ExitCode remains available in Windows PowerShell.
    $null = $explorationProcess.Handle
    $explorationNextHeartbeat = 0d
    while (-not $explorationProcess.WaitForExit(1000)) {
        if ($explorationWatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Unity exceeded the ${TimeoutSeconds}s total deadline in phase: $explorationPhase. Inspect preserved logs."
        }
        if ($explorationWatch.Elapsed.TotalSeconds -ge $explorationNextHeartbeat) {
            $explorationPhase = Get-ExplorationPhase
            $explorationLogAge = if (Test-Path -LiteralPath $explorationLog) {
                [int]([DateTime]::UtcNow - (Get-Item -LiteralPath $explorationLog).LastWriteTimeUtc).TotalSeconds
            } else { -1 }
            $explorationProcess.Refresh()
            Write-ExplorationHeartbeat ("PID={0} elapsed={1:N0}s phase={2}; logAge={3}s CPU={4:N1}s" -f $explorationProcess.Id, $explorationWatch.Elapsed.TotalSeconds, $explorationPhase, $explorationLogAge, $explorationProcess.TotalProcessorTime.TotalSeconds)
            $explorationNextHeartbeat = $explorationWatch.Elapsed.TotalSeconds + $HeartbeatSeconds
        }
    }
    $explorationExitCode = $explorationProcess.ExitCode
    if ($explorationExitCode -ne 0) { throw "Unity exited with code $explorationExitCode; inspect editor.log." }
    if (-not (Test-Path -LiteralPath $explorationResult -PathType Leaf)) { throw 'Unity exited without results.xml.' }
    [xml]$explorationXml = Get-Content -LiteralPath $explorationResult -Raw
    $explorationRun = $explorationXml.'test-run'
    $explorationCases = @($explorationXml.SelectNodes('//test-case'))
    if ($null -eq $explorationRun -or $explorationRun.result -cne 'Passed' -or
        $explorationRun.total -cne '2' -or $explorationRun.passed -cne '2' -or
        $explorationRun.failed -cne '0' -or $explorationRun.skipped -cne '0' -or
        $explorationRun.inconclusive -cne '0' -or $explorationCases.Count -ne 2) {
        throw 'Expected exactly 2 passed tests, with no failures, skipped or inconclusive tests.'
    }
    foreach ($explorationName in $explorationExpected) {
        $explorationMatch = @($explorationCases | Where-Object { $_.fullname -ceq $explorationName -and $_.result -ceq 'Passed' })
        if ($explorationMatch.Count -ne 1) { throw "Missing or non-passing expected test: $explorationName" }
    }
    $explorationStatus = 'Passed'
    Write-ExplorationHeartbeat ("Unity JSON: passed=2 failed=0; exitCode=0; elapsed={0:N1}s" -f $explorationWatch.Elapsed.TotalSeconds)
} catch {
    $explorationFailure = $_.Exception.Message
    Write-ExplorationHeartbeat "FAILED: $explorationFailure"
    throw
} finally {
    # Stop only the exact editor launched by this invocation. Never stop Unity by name,
    # touch another editor, remove a live lock, or delete a Library on timeout.
    if ($null -ne $explorationProcess -and -not $explorationProcess.HasExited) {
        Write-ExplorationHeartbeat "Stopping this runner's Unity PID=$($explorationProcess.Id)."
        $explorationProcess.Kill()
        $null = $explorationProcess.WaitForExit(10000)
    }
    $explorationWatch.Stop()
    if (Test-Path -LiteralPath $explorationUpmLog) {
        Copy-Item -LiteralPath $explorationUpmLog -Destination (Join-Path $explorationOutput 'upm-after.log')
    }
    [pscustomobject]@{
        Status = $explorationStatus; Failure = $explorationFailure; ExitCode = $explorationExitCode
        ElapsedSeconds = [Math]::Round($explorationWatch.Elapsed.TotalSeconds, 3)
        TimeoutSeconds = $TimeoutSeconds; Project = $explorationProject; Result = $explorationResult
        UnityPid = if ($null -ne $explorationProcess) { $explorationProcess.Id } else { $null }
        ExpectedTests = $explorationExpected
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $explorationOutput 'runner-summary.json') -Encoding utf8
}
