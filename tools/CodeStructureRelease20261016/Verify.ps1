param(
    [Parameter(Mandatory=$true)][string]$EditRun,
    [Parameter(Mandatory=$true)][string]$PlayRun,
    [ValidatePattern('^Release[0-9]*$')][string]$SnapshotName = 'Release04'
)
$ErrorActionPreference = 'Stop'
$evidence = 'D:/Game/Artifacts/CodeStructureRelease20261016'
$snapshotDirectory = Join-Path $evidence $SnapshotName
$snapshot = Get-Content -LiteralPath (Join-Path $snapshotDirectory 'snapshot.json') -Raw | ConvertFrom-Json
$sourceManifestHash = (Get-FileHash -LiteralPath (Join-Path $snapshotDirectory 'source-manifest.csv')).Hash
$projectManifestHash = (Get-FileHash -LiteralPath (Join-Path $snapshotDirectory 'project-input-manifest.csv')).Hash
if ($snapshot.Result -ne 'PASS' -or $snapshot.SourceManifestSHA256 -ne $sourceManifestHash -or
    $snapshot.ProjectInputManifestSHA256 -ne $projectManifestHash) { throw 'The release snapshot manifests changed.' }
$cases = [Collections.Generic.List[object]]::new()
$suites = @(foreach ($entry in @(@('EditMode',$EditRun),@('PlayMode',$PlayRun))) {
    $platform = $entry[0]
    $runDirectory = [IO.Path]::GetFullPath($entry[1])
    $launch = Get-Content -LiteralPath (Join-Path $runDirectory 'launch.json') -Raw | ConvertFrom-Json
    $completion = Get-Content -LiteralPath (Join-Path $runDirectory 'completion.json') -Raw | ConvertFrom-Json
    if ($completion.ExitCode -ne 0 -or $launch.Arguments -contains '-testFilter') { throw 'Both full suites must finish successfully.' }
    foreach ($record in @($launch,$completion)) {
        if ($record.InputValidationResult -ne 'PASS' -or $record.SourceManifestSHA256 -ne $sourceManifestHash -or
            $record.ProjectInputManifestSHA256 -ne $projectManifestHash) { throw 'Test inputs were not verified against this release snapshot before and after the run.' }
    }
    $resultsPath = Join-Path $runDirectory 'results.xml'
    $resultsHash = (Get-FileHash -LiteralPath $resultsPath).Hash
    if ($completion.ResultsSHA256 -ne $resultsHash) { throw 'The test result XML changed after completion.' }
    $projectArgument = [Array]::IndexOf($launch.Arguments,'-projectPath')
    $platformArgument = [Array]::IndexOf($launch.Arguments,'-testPlatform')
    if ($projectArgument -lt 0 -or $platformArgument -lt 0 -or
        [IO.Path]::GetFullPath($launch.Arguments[$projectArgument+1]) -ne [IO.Path]::GetFullPath($snapshot.Project) -or
        $launch.Arguments[$platformArgument+1] -ne $platform) { throw 'Test suite does not match the release project.' }
    [xml]$xml = Get-Content -LiteralPath (Join-Path $runDirectory 'results.xml') -Raw
    $run = $xml.'test-run'
    # Unity may label the aggregate Skipped:Ignored when retired fixtures use
    # Ignore, even though every runnable case passed and its process exited 0.
    if ($run.result -notin @('Passed','Skipped:Ignored') -or [int]$run.failed -ne 0 -or
        [int]$run.inconclusive -ne 0 -or [int]$run.passed -le 0 -or
        [int]$run.total -ne ([int]$run.passed + [int]$run.skipped) -or
        $xml.SelectNodes('//test-case[@result!="Passed" and @result!="Skipped"]').Count -gt 0) { throw 'Test suite contains unresolved results.' }
    foreach ($test in $xml.SelectNodes('//test-case')) {
        $reason = $test.SelectSingleNode('reason/message')
        $cases.Add([pscustomobject]@{ Suite=$platform; Name=$test.fullname; Result=$test.result; Reason=$(if($reason){$reason.InnerText}else{''}); Report=(Join-Path $runDirectory 'results.xml') })
    }
    [pscustomobject]@{ Suite=$platform; Total=[int]$run.total; Passed=[int]$run.passed; Failed=[int]$run.failed; Skipped=[int]$run.skipped; Report=$resultsPath; ResultsSHA256=$resultsHash; SourceManifestSHA256=$sourceManifestHash; ProjectInputManifestSHA256=$projectManifestHash }
})
$cases | Export-Csv -LiteralPath (Join-Path $evidence 'verification-cases.csv') -NoTypeInformation -Encoding utf8
$cases | Where-Object Result -ne 'Passed' | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $evidence 'skipped-tests.json') -Encoding utf8
[pscustomobject]@{
    Result='PASS'; RecordedUtc=[DateTime]::UtcNow.ToString('o');
    Passed=($suites|Measure-Object Passed -Sum).Sum;
    Failed=($suites|Measure-Object Failed -Sum).Sum;
    Skipped=($suites|Measure-Object Skipped -Sum).Sum;
    Suites=$suites;
    SourceManifestSHA256=$sourceManifestHash; ProjectInputManifestSHA256=$projectManifestHash;
    Scope='Full EditMode and PlayMode suites in the isolated release project with graphics enabled. Retired/special-condition skips are listed separately. Windows player smoke and ZIP checks follow.'
} | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $evidence 'verification-summary.json')
