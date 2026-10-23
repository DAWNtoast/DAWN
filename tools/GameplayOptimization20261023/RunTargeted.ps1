param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [Parameter(Mandatory=$true)][string]$Output,
    [Parameter(Mandatory=$true)][string]$TestFilter,
    [ValidateSet('EditMode','PlayMode')][string]$Platform = 'PlayMode',
    [string[]]$AdditionalArguments = @()
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$project = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$destination = [IO.Path]::GetFullPath($Output).TrimEnd('\','/')
if (-not $project.StartsWith((Join-Path $workspace 'tmp') + '\',[StringComparison]::OrdinalIgnoreCase) -or
    -not $destination.StartsWith((Join-Path $workspace 'Artifacts') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Use isolated project and artifact paths.' }
if (Test-Path -LiteralPath $destination) { throw 'Use a new output directory.' }
$settings = [IO.File]::ReadAllText((Join-Path $project 'ProjectSettings/ProjectSettings.asset'))
if ($settings -notmatch '(?m)^  companyName: .*Validation.*$') { throw 'Use a validation storage identity.' }
if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($project) }).Count) { throw 'Project already occupied.' }
foreach ($value in $AdditionalArguments) {
    if ($value -in @('-projectPath','-runTests','-testPlatform','-testResults','-logFile','-testFilter','-executeMethod')) { throw 'Additional arguments cannot replace the test invocation.' }
}
$retired = Join-Path $project 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) { throw 'Retired previews must remain empty.' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$manifest = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/_Project') -Recurse -File -Filter '*.cs' | ForEach-Object {
    [pscustomobject]@{Path=$_.FullName.Substring($project.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$manifest | Export-Csv -LiteralPath (Join-Path $destination 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$arguments = @('-batchmode','-force-d3d11','-projectPath',$project,'-runTests','-testPlatform',$Platform,
    '-testFilter',$TestFilter,'-testResults',(Join-Path $destination 'results.xml'),'-logFile',(Join-Path $destination 'unity.log')) + $AdditionalArguments
$quoted = foreach ($value in $arguments) { $escaped = [regex]::Replace($value,'(\\*)"','$1$1\"'); '"' + [regex]::Replace($escaped,'(\\+)$','$1$1') + '"' }
$started = [DateTime]::UtcNow
$process = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList ($quoted -join ' ') -WorkingDirectory $project -WindowStyle Hidden -PassThru
$null = $process.Handle
[pscustomobject]@{PID=$process.Id;Arguments=$arguments;StartedUtc=$started.ToString('o');Scope='Targeted validation only; not full release regression.'} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'launch.json') -Encoding utf8
Write-Output ('Started targeted tests, PID ' + $process.Id)
while (-not $process.WaitForExit(15000)) {
    Write-Output ('Targeted tests running: ' + [int]([DateTime]::UtcNow-$started).TotalSeconds + 's')
    if (([DateTime]::UtcNow-$started).TotalMinutes -gt 20) { $process.Kill(); throw 'Owned targeted test process timed out.' }
}
$changed = @($manifest | Where-Object { (Get-FileHash -LiteralPath (Join-Path $project $_.Path)).Hash -ne $_.SHA256 })
$resultPath = Join-Path $destination 'results.xml'
[pscustomobject]@{ExitCode=$process.ExitCode;Seconds=([DateTime]::UtcNow-$started).TotalSeconds;ChangedInputs=$changed.Count} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'completion.json') -Encoding utf8
if ($changed.Count) { throw 'Inputs changed during targeted tests.' }
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'No result XML; inspect unity.log.' }
[xml]$xml = Get-Content -LiteralPath $resultPath -Raw
$run=$xml.'test-run'
$failures = @($xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
    [pscustomobject]@{Name=$_.fullname;Message=$_.SelectSingleNode('failure/message').InnerText;Stack=$_.SelectSingleNode('failure/stack-trace').InnerText}
})
ConvertTo-Json -InputObject $failures -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'failures.json') -Encoding utf8
[pscustomobject]@{Result=$run.result;Total=[int]$run.total;Passed=[int]$run.passed;Failed=[int]$run.failed;Skipped=[int]$run.skipped;ExitCode=$process.ExitCode} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $destination 'summary.json')
if ($process.ExitCode -ne 0 -or [int]$run.failed -ne 0 -or [int]$run.passed -le 0) { exit 1 }
exit 0
