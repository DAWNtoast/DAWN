[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [ValidateSet('Capture', 'EditMode', 'PlayMode')]
    [string[]]$Checks = @('Capture', 'EditMode', 'PlayMode'),
    [string]$UnityPath = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe',
    [string]$TestFilter
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path.TrimEnd('\', '/')
$editor = (Resolve-Path -LiteralPath $UnityPath).Path
if (-not [IO.Path]::IsPathRooted($OutputPath)) { throw 'OutputPath must be absolute.' }
$output = [IO.Path]::GetFullPath($OutputPath).TrimEnd('\', '/')
if (Test-Path -LiteralPath $output) { throw "OutputPath must be new: $output" }
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt'))) {
    throw "ProjectPath is not a Unity project: $project"
}
if ($output.Equals($project, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($project + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep validation output outside the Unity project.'
}
if ($Checks.Count -ne @($Checks | Select-Object -Unique).Count) { throw 'Checks must not contain duplicates.' }

function Assert-ProjectAvailable {
    try { $unityProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") }
    catch {
        # Restricted hosts can deny CIM even though process enumeration is available.
        # In that case allow only a completely idle editor environment.
        if (@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count) {
            throw 'Cannot inspect Unity project paths; close the existing editor before validation.'
        }
        $unityProcesses = @()
    }
    foreach ($process in $unityProcesses) {
        if ([string]::IsNullOrWhiteSpace($process.CommandLine)) {
            throw "Cannot inspect running Unity process $($process.ProcessId); refusing a concurrent launch."
        }
        $match = [regex]::Match($process.CommandLine, '(?i)(?:^|\s)-projectPath\s+(?:"([^"]+)"|(\S+))')
        if (-not $match.Success) { continue }
        $activePath = if ($match.Groups[1].Success) { $match.Groups[1].Value } else { $match.Groups[2].Value }
        $activePath = [IO.Path]::GetFullPath($activePath).TrimEnd('\', '/')
        if ($activePath.Equals($project, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unity process $($process.ProcessId) already uses $project."
        }
    }
    if (Test-Path -LiteralPath (Join-Path $project 'Temp\UnityLockfile')) {
        throw 'UnityLockfile exists; resolve the existing editor session before validation.'
    }
}

# Start-Process joins ArgumentList on Windows; quote each token explicitly.
function Quote-Argument([string]$Value) {
    if ($Value.Contains('"')) { throw 'Arguments containing a double quote are unsupported.' }
    return '"' + $Value.TrimEnd('\') + '"'
}

Assert-ProjectAvailable
$null = New-Item -ItemType Directory -Path $output
$protectedScene = Join-Path $project 'Assets\_Project\Scenes\UIPreviewsForMe\UI_Battle.unity'
$protectedHash = if (Test-Path -LiteralPath $protectedScene) { (Get-FileHash -LiteralPath $protectedScene -Algorithm SHA256).Hash } else { $null }
$gitHead = (& git -C $project rev-parse HEAD 2>$null | Out-String).Trim()
$gitStatus = @(& git -C $project status --short 2>$null)
$sourceFiles = @(& rg --files (Join-Path $project 'Assets') (Join-Path $project 'Packages') (Join-Path $project 'ProjectSettings') -g '*.cs' -g '*.asmdef' -g '*.json' -g '*.asset' -g 'ProjectVersion.txt')
$sourceManifest = @($sourceFiles | Sort-Object | ForEach-Object {
    @{ path = $_.Substring($project.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
})
ConvertTo-Json -InputObject $sourceManifest -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'source-hashes.json') -Encoding UTF8
$results = [Collections.Generic.List[object]]::new()
@{
    startedAtUtc = [DateTime]::UtcNow.ToString('O'); projectPath = $project; editorPath = $editor
    projectVersion = (Get-Content -Raw -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt')).Trim()
    gitHead = $gitHead; gitStatus = $gitStatus; checks = $Checks; testFilter = $TestFilter
    protectedSceneSha256 = $protectedHash
    sourceManifest = 'source-hashes.json'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'provenance.json') -Encoding UTF8

foreach ($check in $Checks) {
    Assert-ProjectAvailable
    $log = Join-Path $output "$check.log"
    $arguments = @('-batchmode', '-projectPath', $project, '-logFile', $log)
    if ($check -eq 'Capture') {
        $arguments += @('-quit', '-executeMethod',
            'ShatteredSail.Editor.UiAuditReadOnlyCapture.CaptureRefactorBaselineFromCommandLine',
            '-uiAuditPath', (Join-Path $output 'Screenshots'))
    } else {
        $arguments += @('-runTests', '-testPlatform', $check, '-testResults', (Join-Path $output "$check.xml"))
        if ($TestFilter) { $arguments += @('-testFilter', $TestFilter) }
    }
    # Graphics stay enabled for RenderTexture/layout tests. The Test Runner owns exit;
    # only the synchronous capture method receives -quit.
    $arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output "$check-arguments.json") -Encoding UTF8
    Write-Output "Running $check; log: $log"
    $run = Start-Process -FilePath $editor -ArgumentList (($arguments | ForEach-Object { Quote-Argument $_ }) -join ' ') -WindowStyle Hidden -PassThru
    $run.WaitForExit()
    $run.Refresh()
    $record = [ordered]@{ check = $check; exitCode = $run.ExitCode; completedAtUtc = [DateTime]::UtcNow.ToString('O') }
    if ($check -eq 'Capture') {
        $reportPath = Join-Path $output 'Screenshots\report.json'
        if (Test-Path -LiteralPath $reportPath) {
            $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
            $record.frames = @($report.frames).Count
            $record.failedFrames = @($report.frames | Where-Object { $_.error -or -not $_.verifiedScaler }).Count
            $record.passed = $run.ExitCode -eq 0 -and $record.frames -gt 0 -and $record.failedFrames -eq 0
        } else { $record.passed = $false }
    } else {
        $xmlPath = Join-Path $output "$check.xml"
        if (Test-Path -LiteralPath $xmlPath) {
            [xml]$xml = Get-Content -Raw -LiteralPath $xmlPath
            $summary = $xml.'test-run'
            foreach ($key in @('total', 'passed', 'failed', 'skipped')) { $record[$key + 'Tests'] = [int]$summary.$key }
            $record.failedTestNames = @($xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { $_.fullname })
            $record.passed = $run.ExitCode -eq 0 -and [int]$summary.total -gt 0 -and $summary.result -eq 'Passed'
        } else { $record.passed = $false }
    }
    $record.protectedSceneUnchanged = $null -eq $protectedHash -or
        ((Test-Path -LiteralPath $protectedScene) -and (Get-FileHash -LiteralPath $protectedScene -Algorithm SHA256).Hash -eq $protectedHash)
    $results.Add([pscustomobject]$record)
    ConvertTo-Json -InputObject @($results.ToArray()) -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding UTF8
    if (-not $record.protectedSceneUnchanged) { throw "Protected UI_Battle.unity changed during $check; inspect it without restoring over user work." }
}
if (@($results | Where-Object { -not $_.passed }).Count) { throw "Validation has failures; inspect $output\results.json." }
Write-Output "Validation passed: $output"
