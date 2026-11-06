param(
    [switch]$PrepareOnly,
    [switch]$Cleanup,
    [ValidateRange(1,60)][int]$TimeoutMinutes = 15,
    [string]$UnityEditor = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$project = [IO.Path]::GetFullPath((Join-Path $workspace 'tmp/boarding-3d-trial-20261102'))
$output = [IO.Path]::GetFullPath((Join-Path $workspace 'outputs/boarding-3d-trial-20261102'))
$sourceRuntime = Join-Path $workspace 'UnityClient/Assets/_Project/Presentation/Runtime/Boarding3D'
$sourceEditor = Join-Path $workspace 'UnityClient/Assets/_Project/Editor/Boarding3DTrialBuild.cs'
$sourceFont = Join-Path $workspace 'UnityClient/Assets/_Project/Resources/Fonts/HarborBody-Regular.otf'
$exe = Join-Path $output 'playable/Boarding3DTrial.exe'

function Assert-NoLink([string]$Path, [switch]$Recursive) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Refusing linked path: ' + $Path) }
    if ($Recursive) {
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force -Recurse) {
            if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Refusing linked descendant: ' + $child.FullName) }
        }
    }
}

function Assert-ProjectIdle {
    $normalized = $project.Replace('/','\')
    $usingProject = @(Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and $_.Name -match '^(Unity|UnityShaderCompiler|UnityPackageManager|bee_backend|dotnet|il2cpp)\.exe$' -and
        $_.CommandLine.Replace('/','\').IndexOf($normalized,[StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($usingProject.Count) { throw ('Trial project is in use by process(es): ' + (($usingProject.ProcessId) -join ', ')) }
}

function Assert-ProjectBoundary {
    $expected = [IO.Path]::GetFullPath((Join-Path $workspace 'tmp/boarding-3d-trial-20261102')).TrimEnd('\','/')
    if ($project.TrimEnd('\','/') -ne $expected -or $project.StartsWith((Join-Path $workspace 'UnityClient'),[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Trial path is outside the exact permitted disposable project.'
    }
    Assert-NoLink $workspace
    Assert-NoLink (Join-Path $workspace 'tmp')
    Assert-NoLink $project
}

Assert-ProjectBoundary
Assert-ProjectIdle
[IO.Directory]::CreateDirectory($output) | Out-Null

if ($Cleanup) {
    if ($PrepareOnly) { throw 'Use either -PrepareOnly or -Cleanup.' }
    $records = @()
    foreach ($name in @('Library','Temp','obj','Logs')) {
        $target = [IO.Path]::GetFullPath((Join-Path $project $name))
        if (-not $target.StartsWith($project.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
            throw ('Cleanup path escaped the trial project: ' + $target)
        }
        if (-not (Test-Path -LiteralPath $target)) { continue }
        Assert-NoLink $target -Recursive
        Assert-ProjectIdle
        $bytes = (Get-ChildItem -LiteralPath $target -File -Force -Recurse | Measure-Object Length -Sum).Sum
        if ($null -eq $bytes) { $bytes = 0 }
        # Only regenerable caches in this task's exact isolated project are removed.
        Remove-Item -LiteralPath $target -Force -Recurse
        if (Test-Path -LiteralPath $target) { throw ('Cleanup did not remove ' + $target) }
        $records += [pscustomobject]@{ Path=$target; Bytes=[long]$bytes; Removed=$true }
    }
    $report = [pscustomobject]@{
        CompletedUtc=[DateTime]::UtcNow.ToString('o'); Project=$project; Removed=$records
        BytesFreed=[long](($records | Measure-Object Bytes -Sum).Sum)
        Retained=@('Isolated Assets/Packages/ProjectSettings for reproduction','Source snapshots and build logs under outputs','Playable and preview artifacts','Shared boarding-video-tools ffmpeg cache')
        MainProjectTouched=$false
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'cleanup.json') -Encoding utf8
    $report | ConvertTo-Json -Depth 5
    exit 0
}

if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw ('Unity Editor missing: ' + $UnityEditor) }
foreach ($required in @('Boarding3DActor.cs','Boarding3DTrial.cs','Boarding3DTrialCapture.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRuntime $required) -PathType Leaf)) { throw ('Missing trial source: ' + $required) }
}
if (-not (Test-Path -LiteralPath $sourceEditor -PathType Leaf)) { throw 'Missing Boarding3DTrialBuild.cs' }
foreach ($fontInput in @($sourceFont,($sourceFont + '.meta'))) {
    if (-not (Test-Path -LiteralPath $fontInput -PathType Leaf)) { throw ('Missing production font input: ' + $fontInput) }
    Assert-NoLink $fontInput
}
Assert-NoLink $sourceRuntime -Recursive
Assert-NoLink $sourceEditor
Assert-NoLink $project -Recursive

$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$run = Join-Path $output ('runs/' + $runId)
$snapshot = Join-Path $run 'source-snapshot'
$destinationRuntime = Join-Path $project 'Assets/Trial/Runtime'
$destinationEditor = Join-Path $project 'Assets/Trial/Editor'
$destinationFonts = Join-Path $project 'Assets/Resources/Fonts'
foreach ($directory in @($run,$snapshot,$destinationRuntime,$destinationEditor,$destinationFonts,(Join-Path $project 'Packages'),(Join-Path $project 'ProjectSettings'),(Join-Path $output 'playable'))) {
    [IO.Directory]::CreateDirectory($directory) | Out-Null
}
# The source selection is deliberately narrow; no production scenes, packages or caches are copied.
Copy-Item -LiteralPath $sourceRuntime -Destination (Join-Path $snapshot 'Runtime') -Recurse
Copy-Item -LiteralPath $sourceEditor -Destination (Join-Path $snapshot 'Boarding3DTrialBuild.cs')
if (Test-Path -LiteralPath ($sourceEditor + '.meta')) { Copy-Item -LiteralPath ($sourceEditor + '.meta') -Destination (Join-Path $snapshot 'Boarding3DTrialBuild.cs.meta') }
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $snapshot 'BuildTrial.ps1')
Get-ChildItem -LiteralPath $sourceRuntime -Force | Copy-Item -Destination $destinationRuntime -Recurse -Force
Copy-Item -LiteralPath $sourceEditor -Destination (Join-Path $destinationEditor 'Boarding3DTrialBuild.cs') -Force
if (Test-Path -LiteralPath ($sourceEditor + '.meta')) { Copy-Item -LiteralPath ($sourceEditor + '.meta') -Destination (Join-Path $destinationEditor 'Boarding3DTrialBuild.cs.meta') -Force }
Copy-Item -LiteralPath (Join-Path $workspace 'UnityClient/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Force
# Reference the existing reviewed font instead of storing another 8 MB copy in every run snapshot.
# The isolated build still receives the exact production font and importer metadata.
$fontInputs = foreach ($fontInput in @($sourceFont,($sourceFont + '.meta'))) {
    $targetFont = Join-Path $destinationFonts ([IO.Path]::GetFileName($fontInput))
    $fontHash = (Get-FileHash -LiteralPath $fontInput -Algorithm SHA256).Hash
    if (-not (Test-Path -LiteralPath $targetFont) -or (Get-FileHash -LiteralPath $targetFont -Algorithm SHA256).Hash -ne $fontHash) {
        Copy-Item -LiteralPath $fontInput -Destination $targetFont -Force
    }
    [pscustomobject]@{ Source=$fontInput; IsolatedPath=$targetFont; Bytes=(Get-Item -LiteralPath $fontInput).Length; SHA256=$fontHash }
}
$fontInputs | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $snapshot 'production-font-inputs.json') -Encoding utf8

$manifest = @{
    dependencies = [ordered]@{
        'com.unity.ugui'='2.0.0'
        'com.unity.modules.animation'='1.0.0'
        'com.unity.modules.audio'='1.0.0'
        'com.unity.modules.imageconversion'='1.0.0'
        'com.unity.modules.imgui'='1.0.0'
        'com.unity.modules.jsonserialize'='1.0.0'
        'com.unity.modules.physics'='1.0.0'
        'com.unity.modules.screencapture'='1.0.0'
        'com.unity.modules.ui'='1.0.0'
    }
} | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),$manifest,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $project 'Packages/manifest.json') -Destination (Join-Path $snapshot 'manifest.json')
Copy-Item -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $snapshot 'ProjectVersion.txt')
$inputs = @(Get-ChildItem -LiteralPath $snapshot -File -Recurse | ForEach-Object {
    [pscustomobject]@{ Path=$_.FullName.Substring($snapshot.Length + 1).Replace('\','/'); Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$inputs | Export-Csv -LiteralPath (Join-Path $run 'input-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) {
    [pscustomobject]@{ Status='PREPARED'; Project=$project; Snapshot=$snapshot; Executable=$exe } | ConvertTo-Json
    exit 0
}

$log = Join-Path $run 'unity-build.log'
$arguments = @('-batchmode','-force-d3d11','-projectPath',$project,'-executeMethod','ShatteredSail.Editor.Boarding3DTrialBuild.BuildFromCommandLine','-trialBuildOutput',$exe,'-logFile',$log,'-quit')
$quoted = foreach ($value in $arguments) {
    $escaped = [regex]::Replace($value,'(\\*)"','$1$1\"')
    '"' + [regex]::Replace($escaped,'(\\+)$','$1$1') + '"'
}
$started = [DateTime]::UtcNow
$process = Start-Process -FilePath $UnityEditor -ArgumentList ($quoted -join ' ') -WorkingDirectory $project -WindowStyle Hidden -PassThru
$null = $process.Handle
[pscustomobject]@{ PID=$process.Id; StartedUtc=$started.ToString('o'); Arguments=$arguments; Snapshot=$snapshot; TimeoutMinutes=$TimeoutMinutes } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'launch.json') -Encoding utf8
Write-Output ('3D trial build started. PID=' + $process.Id + ' Log=' + $log)
$success = $false
try {
    while (-not $process.WaitForExit(15000)) {
        Write-Output ('3D trial build running: ' + [int]([DateTime]::UtcNow - $started).TotalSeconds + 's')
        if (([DateTime]::UtcNow - $started).TotalMinutes -gt $TimeoutMinutes) {
            $process.Kill()
            $process.WaitForExit()
            throw 'Owned Unity build process timed out. Logs and source snapshot have been retained.'
        }
    }
    if ($process.ExitCode -ne 0) { throw ('Unity build failed with exit code ' + $process.ExitCode + '. See ' + $log) }
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or (Get-Item -LiteralPath $exe).Length -le 0) {
        throw 'Unity did not produce a playable executable.'
    }
    # Incremental builds can retain the native player executable and its timestamp.
    # The editor entry point writes this report only after BuildPipeline succeeds.
    $buildSummaryPath = Join-Path (Split-Path -Parent $exe) 'build-summary.json'
    if (-not (Test-Path -LiteralPath $buildSummaryPath -PathType Leaf) -or
        (Get-Item -LiteralPath $buildSummaryPath).LastWriteTimeUtc -lt $started) {
        throw 'Unity did not produce a fresh build-summary.json for this run.'
    }
    $buildSummary = Get-Content -LiteralPath $buildSummaryPath -Raw | ConvertFrom-Json
    if ($buildSummary.result -cne 'PASS' -or $null -eq $buildSummary.errors -or [int]$buildSummary.errors -ne 0) {
        throw 'The fresh Unity build summary did not report PASS with zero errors.'
    }
    Copy-Item -LiteralPath $buildSummaryPath -Destination (Join-Path $run 'build-summary.json')
    $success = $true
} finally {
    if (Test-Path -LiteralPath $log) { Copy-Item -LiteralPath $log -Destination (Join-Path $output 'build.log') -Force }
    [pscustomobject]@{ Success=$success; ExitCode=$process.ExitCode; Seconds=([DateTime]::UtcNow - $started).TotalSeconds; Project=$project; Executable=$exe; Snapshot=$snapshot; Log=$log } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'completion.json') -Encoding utf8
}
Get-ChildItem -LiteralPath (Join-Path $output 'playable') -File -Recurse | ForEach-Object {
    [pscustomobject]@{ Path=$_.FullName.Substring($output.Length + 1).Replace('\','/'); Bytes=$_.Length; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
} | Export-Csv -LiteralPath (Join-Path $run 'playable-hashes.csv') -NoTypeInformation -Encoding utf8
Write-Output ('Playable ready: ' + $exe)
Write-Output 'Isolated caches retained for iteration. Run this script with -Cleanup after capture and validation are complete.'
