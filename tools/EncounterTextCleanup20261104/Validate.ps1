param(
 [string[]]$Fixtures=@('CoreVoyageEventCatalogTests','CoreEventChoiceCatalogAuditTests','CoreVoyageEventConditionTests','CoreTreasureExpeditionMechanicsTests','CoreVoyageExplorationTests','CoreSeaChartExhaustedRewardTests'),
 [string]$UnityData='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference='Stop'
foreach($testFixture in $Fixtures){if($testFixture -notmatch '^[a-zA-Z0-9_]+$'){throw 'Fixture names must be simple identifiers.'}}
$testWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$testOutput=Join-Path $testWorkspace 'outputs/encounter-text-cleanup-20261104/validation'
$testBuild=Join-Path $testOutput ('TestBuild-'+[Guid]::NewGuid().ToString('N'))
$testCreated=$false
try {
 New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
 New-Item -ItemType Directory -Path $testBuild | Out-Null
 $testCreated=$true
 $testFramework=Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
 $testNunit=Get-ChildItem -LiteralPath (Join-Path $testWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' | ForEach-Object {Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
 if(!$testNunit){throw 'NUnit assembly not available in the existing package cache.'}
 $testSources=@(Get-ChildItem -LiteralPath (Join-Path $testWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -Recurse | Sort-Object FullName)
 $testSources+=Get-Item -LiteralPath (Join-Path $testWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
 $testSources+=Get-Item -LiteralPath (Join-Path $testWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
 foreach($testName in @('CardSpecialCostPresentation.cs','CardSupplyCostPresentation.cs','CardOrnamentKind.cs','CoreTreasureStoryCatalog.cs','CoreGameplaySeaChartPresentation.cs','CoreGameplayExplorationPresentation.cs')){
  $testSources+=Get-Item -LiteralPath (Join-Path $testWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/'+$testName))
 }
 $testSources+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'PresentationProbe.cs')
 foreach($testFixture in $Fixtures){$testSources+=Get-Item -LiteralPath (Join-Path $testWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/'+$testFixture+'.cs'))}
 @($Fixtures)+@('EncounterTextPresentationProbeTests') | Set-Content -LiteralPath (Join-Path $testOutput 'fixtures.txt') -Encoding utf8
 $testHashes=@($testSources | ForEach-Object{[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 $testHashes | Export-Csv -LiteralPath (Join-Path $testOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
 Copy-Item -LiteralPath $testNunit -Destination (Join-Path $testBuild 'nunit.framework.dll')
 $testExe=Join-Path $testBuild 'EncounterTextTests.exe'
 $testArgs=@((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$testExe","-r:$testNunit")
 foreach($testAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')){$testArgs+='-r:'+(Join-Path $testFramework $testAssembly)}
 $testArgs+=$testSources.FullName
 & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @testArgs 2>&1 | Tee-Object -FilePath (Join-Path $testOutput 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Encounter text regression compilation failed.'}
 & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $testExe $testOutput 2>&1 | Tee-Object -FilePath (Join-Path $testOutput 'tests.log')
 $testExit=$LASTEXITCODE
 foreach($testHash in $testHashes){if((Get-FileHash -LiteralPath $testHash.Source -Algorithm SHA256).Hash -ne $testHash.SHA256){throw ('Source changed during regression: '+$testHash.Source)}}
 if($testExit -ne 0){throw 'Encounter text regression failed; inspect failures.txt and results.xml.'}
} finally {
 if($testCreated -and (Test-Path -LiteralPath $testBuild)){
  try {
   $testCleanup=[IO.Path]::GetFullPath($testBuild).TrimEnd('\','/')
   if([IO.Path]::GetDirectoryName($testCleanup) -ne [IO.Path]::GetFullPath($testOutput).TrimEnd('\','/') -or [IO.Path]::GetFileName($testCleanup) -notmatch '^TestBuild-[a-f0-9]{32}$'){throw 'Unsafe test cleanup target.'}
   $testAncestor=Get-Item -LiteralPath $testCleanup -Force
   while($null -ne $testAncestor){if(($testAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Cleanup refuses directory links.'};$testAncestor=$testAncestor.Parent}
   $testItems=@(Get-ChildItem -LiteralPath $testCleanup -Force)
   foreach($testItem in $testItems){if($testItem.PSIsContainer -or ($testItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $testItem.Name -notin @('EncounterTextTests.exe','nunit.framework.dll')){throw ('Unexpected build content: '+$testItem.FullName)}}
   $testProcesses=@(Get-CimInstance Win32_Process | Where-Object{$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.IndexOf($testCleanup,[StringComparison]::OrdinalIgnoreCase) -ge 0})
   if($testProcesses.Count -gt 0){throw 'Test output still referenced by another process.'}
   $testReleased=0L
   foreach($testItem in $testItems){$testStream=[IO.File]::Open($testItem.FullName,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$testStream.Dispose();$testReleased+=$testItem.Length;Remove-Item -LiteralPath $testItem.FullName -Force}
   [IO.Directory]::Delete($testCleanup,$false)
   if(Test-Path -LiteralPath $testCleanup){throw 'Test output still exists after cleanup.'}
   [pscustomobject]@{Path=$testCleanup;Removed=$true;ReleasedBytes=$testReleased;Retained='NUnit XML, fixtures, failures, logs and source hashes'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testOutput 'cleanup.json') -Encoding utf8
  } catch {Write-Warning ('Test build retained for inspection: '+$testBuild+'. '+$_.Exception.Message)}
 }
}
