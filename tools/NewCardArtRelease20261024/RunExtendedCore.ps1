param([string]$RunTag='ExtendedCore01')
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=Join-Path $workspace ('Artifacts/NewCardArtRelease20261024/'+$RunTag)
if(Test-Path -LiteralPath $output){throw 'Preserve prior diagnostics; use a new RunTag.'}
$testRoot=Join-Path $workspace 'UnityClient/Assets/_Project/Tests/EditMode'
$covered=@(Get-Content -LiteralPath (Join-Path $workspace 'Artifacts/BoardingFieldBalance20261024/FinalProductionNoOverlay/fixtures.txt'))
$fixtures=@(Get-ChildItem -LiteralPath $testRoot -Filter '*.cs' -File | Where-Object {
    $content=[IO.File]::ReadAllText($_.FullName)
    $_.Name -notin $covered -and $content -match 'namespace ShatteredSail\.Core\.Tests' -and
    $content -notmatch '(?m)^using (Unity|ShatteredSail\.(Presentation|Content|Editor|Persistence|Infrastructure))' -and
    $content -notmatch '\b(UnityEngine|UnityEditor|UnityTest|LegacyTreasureVoyageFixture)\b'
} | Sort-Object Name)
$fixtures+=Get-Item -LiteralPath (Join-Path $testRoot 'CoreGuidedBossScriptTests.cs')
$unity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$framework=Join-Path $unity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$nunit=Get-ChildItem -LiteralPath (Join-Path $workspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object {Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
$sources=@(Get-ChildItem -LiteralPath (Join-Path $workspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -File | Sort-Object Name)
$sources+=$fixtures
$sources+=Get-Item -LiteralPath (Join-Path $workspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$sources+=Get-Item -LiteralPath (Join-Path $workspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
$build=Join-Path $output 'Build'
[IO.Directory]::CreateDirectory($build)|Out-Null
$hashes=@($sources|ForEach-Object{[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$hashes|Export-Csv -LiteralPath (Join-Path $output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$fixtures.Name|Set-Content -LiteralPath (Join-Path $output 'fixtures.txt') -Encoding utf8
Copy-Item -LiteralPath $nunit -Destination (Join-Path $build 'nunit.framework.dll')
$executable=Join-Path $build 'ExtendedCoreTests.exe'
$compile=@((Join-Path $unity 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',('-out:'+$executable),('-r:'+$nunit))
foreach($assembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')){$compile+=('-r:'+(Join-Path $framework $assembly))}
$compile+=$sources.FullName
& (Join-Path $unity 'NetCoreRuntime/dotnet.exe') @compile 2>&1|Tee-Object -FilePath (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Extended Core diagnostic compilation failed; inspect compile.log.'}
& (Join-Path $unity 'MonoBleedingEdge/bin/mono.exe') $executable $output 2>&1|Tee-Object -FilePath (Join-Path $output 'tests.log')
$testExit=$LASTEXITCODE
foreach($record in $hashes){if((Get-FileHash -LiteralPath $record.Source).Hash -ne $record.SHA256){throw ('Source changed during diagnostic: '+$record.Source)}}
[xml]$xml=Get-Content -LiteralPath (Join-Path $output 'results.xml') -Raw
$result=$xml.DocumentElement
[pscustomobject]@{Result=$result.result;Fixtures=$fixtures.Count;Total=[int]$result.total;Passed=[int]$result.passed;Failed=[int]$result.failed;Skipped=[int]$result.skipped;InputHashesStable=$true;Scope='Additional standalone Core fixtures outside the 81-fixture boarding regression. This is a fast diagnostic and does not replace full Unity release suites.'}|ConvertTo-Json|Tee-Object -FilePath (Join-Path $output 'summary.json')
exit $testExit
