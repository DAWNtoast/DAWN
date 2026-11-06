param(
    [Parameter(Mandatory=$true)][ValidateSet('Snapshot','Tests')][string]$Stage,
    [ValidatePattern('^[0-9]{2}$')][string]$Revision='01',
    [string[]]$FixtureFiles=@('BattleRetainedTurnTests.cs','BattleSecondBatchMotionTests.cs','DepartureCrewCardPreviewTests.cs',
        'SchoolCardBattleFlowTests.cs','FirstCraftedTreasureAdventureAcceptanceTests.cs'),
    [ValidatePattern('^Play[0-9]{2}$')][string]$PriorFailureRun='Play04',
    [ValidatePattern('^[A-Za-z0-9.]*$')][string]$TradeAcceptanceSeed=''
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$source=Join-Path $workspace 'UnityClient'
$project=Join-Path $workspace ('tmp/new-card-art-play-audit-20261024-'+$Revision)
$evidence=Join-Path $workspace ('Artifacts/NewCardArtRelease20261024/PlayAuditSnapshot'+$Revision)
$output=Join-Path $workspace ('Artifacts/NewCardArtRelease20261024/PlayAudit'+$Revision)
$fixtures=$FixtureFiles
foreach($file in $fixtures){if($file -notmatch '^[A-Za-z0-9]+\.cs$'){throw 'Use an explicit PlayMode fixture file name.'}}
if($Stage -eq 'Snapshot'){
    foreach($path in @($project,$evidence,$output)){if(Test-Path -LiteralPath $path){throw ('Preserve prior diagnostics: '+$path)}}
    foreach($part in @('Assets','Packages','ProjectSettings')){[IO.Directory]::CreateDirectory((Join-Path $project $part))|Out-Null}
    Copy-Item -LiteralPath (Join-Path $source 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $project 'ProjectSettings/ProjectVersion.txt')
    & (Join-Path $workspace 'tools/ReleaseBuild/DesktopRelease.ps1') -Stage Snapshot -ProjectPath $project `
        -SourceProjectPath $source -EvidenceDirectory $evidence -ReleaseDirectory (Join-Path $workspace ('Builds/NewCardArt-PlayDiagnostics-20261024-'+$Revision)) `
        -Version '0.3.20261024.1' -ZipFileName 'diagnostics-not-a-release.zip' -ValidationSummary (Join-Path $evidence 'NOT_A_RELEASE.json')
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
    $methods=@(foreach($file in $fixtures){
        $text=[IO.File]::ReadAllText((Join-Path $project ('Assets/_Project/Tests/PlayMode/'+$file)))
        $matches=[regex]::Matches($text,'\[UnityTest[^\]]*\](?:\s*\[[^\]]*\])*\s*public\s+IEnumerator\s+(\w+)\s*\(')
        if($matches.Count -eq 0){throw ('No diagnostic tests found in '+$file)}
        foreach($match in $matches){
            'ShatteredSail.Presentation.Tests.CoreGameplayDemoSceneTests.'+$match.Groups[1].Value
        }
    })
    if($methods.Count -eq 0){throw 'Expected failed methods and their same-file regression neighbors.'}
    $priorFailures=Get-Content -LiteralPath (Join-Path $workspace ('Artifacts/NewCardArtRelease20261024/'+$PriorFailureRun+'/failures.json')) -Raw|ConvertFrom-Json
    foreach($failure in $priorFailures){if($methods -notcontains $failure.Name){throw ('Missing failed method: '+$failure.Name)}}
    $methods|Set-Content -LiteralPath (Join-Path $evidence 'diagnostic-methods.txt') -Encoding utf8
    [pscustomobject]@{Scope='DIAGNOSTIC_ONLY_NOT_RELEASE_VALIDATION';Fixtures=$fixtures;Methods=$methods.Count;Project=$project;PriorFailures=$PriorFailureRun;TradeAcceptanceSeed=$TradeAcceptanceSeed}|ConvertTo-Json -Depth 3|Tee-Object -FilePath (Join-Path $evidence 'diagnostic-scope.json')
    exit 0
}
$methods=@(Get-Content -LiteralPath (Join-Path $evidence 'diagnostic-methods.txt'))
$scope=Get-Content -LiteralPath (Join-Path $evidence 'diagnostic-scope.json') -Raw|ConvertFrom-Json
& (Join-Path $workspace 'tools/ReleaseBuild/RunTests.ps1') -Platform PlayMode -ProjectPath $project -Output $output `
    -SourceManifest (Join-Path $evidence 'source-manifest.csv') -TestFilter ($methods -join ';') -TradeAcceptanceSeed $scope.TradeAcceptanceSeed -TimeoutMinutes 90
exit $LASTEXITCODE
