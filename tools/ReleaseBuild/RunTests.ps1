param(
    [Parameter(Mandatory=$true)][ValidateSet('EditMode','PlayMode')][string]$Platform,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$TestFilter = '',
    [string]$SourceManifest = '',
    [ValidateRange(1,240)][int]$TimeoutMinutes = 60,
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$project = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$destination = [IO.Path]::GetFullPath($Output).TrimEnd('\','/')
$sourceManifestHash = $null
$projectManifestHash = $null
$projectManifestPath = $null
function Assert-ReleaseInputs {
    if ((Get-FileHash -LiteralPath $SourceManifest).Hash -ne $sourceManifestHash -or
        (Get-FileHash -LiteralPath $projectManifestPath).Hash -ne $projectManifestHash) { throw 'The locked input manifest changed during testing.' }
    $manifest = @(Import-Csv -LiteralPath $projectManifestPath)
    if (-not $manifest.Count) { throw 'An input manifest must not be empty.' }
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest) {
        $relative = $file.Path.Replace('\','/')
        if ($relative -notmatch '^(Assets|Packages|ProjectSettings)/' -or $relative -match '(^|/)\.\.?(/|$)' -or
            $file.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or -not $expected.Add($relative)) { throw ('Invalid or duplicate manifest entry: ' + $relative) }
        $target = [IO.Path]::GetFullPath((Join-Path $project $relative))
        if (-not $target.StartsWith($project + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Input manifest path escaped the isolated project.' }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) { throw ('Test input differs from the release snapshot: ' + $target) }
    }
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $project $part) -File -Recurse -Force) {
            $relative = $file.FullName.Substring($project.Length+1).Replace('\','/')
            if ($relative -match '(^|/)UIPreviewsForMe(?:/|\.|$)') { continue }
            if (-not $expected.Contains($relative)) { throw ('Unexpected test input outside the release snapshot: ' + $file.FullName) }
        }
    }
    $retiredInputs = Join-Path $project 'Assets/_Project/Scenes/UIPreviewsForMe'
    if ((Test-Path -LiteralPath $retiredInputs) -and @(Get-ChildItem -LiteralPath $retiredInputs -Force).Count) { throw 'Retired previews must remain empty.' }
}
if (-not $project.StartsWith((Join-Path $workspace 'tmp') + '\',[StringComparison]::OrdinalIgnoreCase) -or
    -not $destination.StartsWith((Join-Path $workspace 'Artifacts') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated workspace/tmp project and workspace/Artifacts output.' }
if (Test-Path -LiteralPath $destination) { throw 'Use a new output directory to preserve evidence.' }
$retired = Join-Path $project 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) { throw 'Retired previews must remain empty.' }
$settings = [IO.File]::ReadAllText((Join-Path $project 'ProjectSettings/ProjectSettings.asset'))
if ($settings -notmatch '(?m)^  companyName: .*Validation.*$') { throw 'Use a validation storage identity to protect normal player saves.' }
if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($project.Replace('/','\')) }).Count) { throw 'The isolated project is already running.' }
if ($SourceManifest) {
    $SourceManifest = [IO.Path]::GetFullPath($SourceManifest)
    $snapshotDirectory = [IO.Path]::GetDirectoryName($SourceManifest)
    $snapshot = Get-Content -LiteralPath (Join-Path $snapshotDirectory 'snapshot.json') -Raw | ConvertFrom-Json
    $projectManifestPath = Join-Path $snapshotDirectory 'project-input-manifest.csv'
    $sourceManifestHash = (Get-FileHash -LiteralPath $SourceManifest).Hash
    $projectManifestHash = (Get-FileHash -LiteralPath $projectManifestPath).Hash
    if ($snapshot.Result -ne 'PASS' -or [IO.Path]::GetFullPath($snapshot.Project).TrimEnd('\','/') -ne $project -or
        $snapshot.SourceManifestSHA256 -ne $sourceManifestHash -or $snapshot.ProjectInputManifestSHA256 -ne $projectManifestHash) { throw 'The source manifest does not identify this isolated release snapshot.' }
    Assert-ReleaseInputs
}
& (Join-Path $PSScriptRoot 'AssertMetaGuids.ps1') -AssetsPath (Join-Path $project 'Assets')
[IO.Directory]::CreateDirectory($destination) | Out-Null
# Some EditMode tests render real UI to RenderTextures, so both suites need graphics.
$arguments = @('-batchmode','-force-d3d11','-projectPath',$project,'-runTests','-testPlatform',$Platform,
    '-testResults',(Join-Path $destination 'results.xml'),'-logFile',(Join-Path $destination 'unity.log'))
if ($TestFilter) { $arguments += @('-testFilter',$TestFilter) }
if ($Platform -eq 'PlayMode') { $arguments += @('-audioCapturePath',(Join-Path $destination 'AudioCapture'),'-auditActionLogPath',(Join-Path $destination 'Actions')) }
$quoted = foreach ($value in $arguments) { $escaped = [regex]::Replace($value,'(\\*)"','$1$1\"'); '"' + [regex]::Replace($escaped,'(\\+)$','$1$1') + '"' }
$started = [DateTime]::UtcNow
$process = Start-Process -FilePath $UnityEditor -ArgumentList ($quoted -join ' ') -WorkingDirectory $project -WindowStyle Hidden -PassThru
$null = $process.Handle
[pscustomobject]@{ PID=$process.Id; Arguments=$arguments; StartedUtc=$started.ToString('o'); TimeoutMinutes=$TimeoutMinutes; SourceManifest=$SourceManifest; SourceManifestSHA256=$sourceManifestHash; ProjectInputManifestSHA256=$projectManifestHash; InputValidationResult=$(if($SourceManifest){'PASS'}else{'NOT_REQUESTED'}) } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'launch.json') -Encoding utf8
Write-Output ($Platform + ' started, PID ' + $process.Id)
while (-not $process.WaitForExit(15000)) {
    Write-Output ($Platform + ' running: ' + [int]([DateTime]::UtcNow-$started).TotalSeconds + 's')
    if (([DateTime]::UtcNow-$started).TotalMinutes -gt $TimeoutMinutes) { $process.Kill(); throw 'Owned test process timed out; evidence preserved.' }
}
$inputValidationResult = 'NOT_REQUESTED'
$inputValidationError = $null
if ($SourceManifest) {
    try { Assert-ReleaseInputs; $inputValidationResult = 'PASS' }
    catch { $inputValidationResult = 'FAIL'; $inputValidationError = $_.Exception.Message }
}
$resultsPath = Join-Path $destination 'results.xml'
$resultsHash = if (Test-Path -LiteralPath $resultsPath -PathType Leaf) { (Get-FileHash -LiteralPath $resultsPath).Hash } else { $null }
[pscustomobject]@{ ExitCode=$process.ExitCode; Seconds=([DateTime]::UtcNow-$started).TotalSeconds; SourceManifestSHA256=$sourceManifestHash; ProjectInputManifestSHA256=$projectManifestHash; InputValidationResult=$inputValidationResult; InputValidationError=$inputValidationError; ResultsSHA256=$resultsHash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'completion.json') -Encoding utf8
if ($inputValidationResult -eq 'FAIL') { throw $inputValidationError }
if (-not (Test-Path -LiteralPath $resultsPath -PathType Leaf)) {
    throw ('Unity ' + $Platform + ' exited with code ' + $process.ExitCode +
        ' without producing test results: ' + $resultsPath +
        '. Inspect the Unity log: ' + (Join-Path $destination 'unity.log'))
}
[xml]$xml = Get-Content -LiteralPath (Join-Path $destination 'results.xml') -Raw
$run = $xml.'test-run'
$failures = @($xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
    [pscustomobject]@{ Name=$_.fullname; Message=$_.SelectSingleNode('failure/message').InnerText; Stack=$_.SelectSingleNode('failure/stack-trace').InnerText }
})
$failures | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'failures.json') -Encoding utf8
$summary = [pscustomobject]@{ Result=$run.result; Total=[int]$run.total; Passed=[int]$run.passed; Failed=[int]$run.failed; Skipped=[int]$run.skipped; ExitCode=$process.ExitCode }
$summary | ConvertTo-Json | Tee-Object -FilePath (Join-Path $destination 'summary.json')
if ($process.ExitCode -ne 0 -or [int]$run.failed -ne 0 -or [int]$run.passed -le 0) { exit 1 }
exit 0
