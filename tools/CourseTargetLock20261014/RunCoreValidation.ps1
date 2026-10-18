param(
    [string]$RunTag='CurrentCore',
    [string]$UnityData='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$ArtifactDirectory='Artifacts/CourseTargetLock20261014',
    [string[]]$AdditionalFixtures=@()
)
$ErrorActionPreference='Stop'
if($RunTag-notmatch'^[A-Za-z0-9_-]+$'){throw 'RunTag must be a simple identifier.'}
$courseWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$courseOutputRoot=[IO.Path]::GetFullPath((Join-Path $courseWorkspace $ArtifactDirectory))
if(-not$courseOutputRoot.StartsWith((Join-Path $courseWorkspace 'Artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Results must remain in the workspace Artifacts directory.'}
$courseOutput=Join-Path $courseOutputRoot $RunTag
$courseBuild=Join-Path $courseOutput 'Build'
New-Item -ItemType Directory -Force -Path $courseBuild|Out-Null
$courseFramework=Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$courseNunit=Get-ChildItem -LiteralPath (Join-Path $courseWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*'|
    ForEach-Object{Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll'}|
    Where-Object{Test-Path -LiteralPath $_}|Select-Object -First 1
if(-not$courseNunit){throw 'The existing local Unity NUnit cache is required.'}
Copy-Item -LiteralPath $courseNunit -Destination (Join-Path $courseBuild 'nunit.framework.dll') -Force
$courseSources=@(Get-ChildItem -LiteralPath (Join-Path $courseWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs'|Sort-Object Name)
$courseFixtures=@('CoreVoyageSessionTests.cs','CorePortArrivalTests.cs','EncounterDiscoveryRulesTests.cs')+$AdditionalFixtures
foreach($courseFixture in $courseFixtures){
    $courseFile=Get-Item -LiteralPath (Join-Path $courseWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/'+$courseFixture))
    if([IO.File]::ReadAllText($courseFile.FullName)-match'UnityEngine|UnityEditor|ShatteredSail.Presentation'){throw "Use Unity for this whole fixture: $courseFixture"}
    $courseSources+=$courseFile
}
$courseSources+=Get-Item -LiteralPath (Join-Path $courseWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$courseSources+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'CoreRunner.cs')
$courseHashes=@($courseSources|ForEach-Object{[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
$courseHashes|Export-Csv -LiteralPath (Join-Path $courseOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$courseFixtures|Set-Content -LiteralPath (Join-Path $courseOutput 'fixtures.txt') -Encoding utf8
'Full current Core sources plus selected whole NUnit fixtures. No rewritten assertions, no event density benchmark, no Unity editor, no player profile. Actual Sail and checkpoint coverage depends on named fixtures. Pure Core checkpoint checks are DTO round trips; actual JsonUtility is covered separately in isolated Unity.'|Set-Content -LiteralPath (Join-Path $courseOutput 'scope.txt') -Encoding utf8
$courseExe=Join-Path $courseBuild 'CourseTargetLockValidation.exe'
$courseArguments=@((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$courseExe","-r:$courseNunit")
foreach($courseAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')){$courseArguments+='-r:'+(Join-Path $courseFramework $courseAssembly)}
$courseArguments+=$courseSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @courseArguments 2>&1|Tee-Object -FilePath (Join-Path $courseOutput 'compile.log')
if($LASTEXITCODE-ne0){throw 'Course target lock Core compilation failed.'}
foreach($courseHash in $courseHashes){if((Get-FileHash -LiteralPath $courseHash.Source -Algorithm SHA256).Hash-ne$courseHash.SHA256){throw "Source changed during compilation: $($courseHash.Source)"}}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $courseExe $courseOutput 2>&1|Tee-Object -FilePath (Join-Path $courseOutput 'tests.log')
if($LASTEXITCODE-ne0){throw 'Course target lock tests failed; inspect results.xml and failures.txt.'}
