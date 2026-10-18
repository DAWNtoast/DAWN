param(
    [string]$Output='D:/Game/Artifacts/VoyageEventExpansion20261014/Unity',
    [string]$UnityEditor='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [switch]$PrepareOnly
)
$ErrorActionPreference='Stop'
$eventWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$eventProject=[IO.Path]::GetFullPath((Join-Path $eventWorkspace 'tmp/voyage-event-expansion-20261014'))
$eventMain=Join-Path $eventWorkspace 'UnityClient'
$eventOutput=[IO.Path]::GetFullPath($Output)
$eventMarker=Join-Path $eventProject 'VOYAGE_EVENT_VALIDATION_20261014.txt'
if ((Test-Path -LiteralPath $eventProject) -and -not (Test-Path -LiteralPath $eventMarker)) { throw 'Refuse to reuse an existing unowned isolation directory.' }
if (Test-Path -LiteralPath (Join-Path $eventProject 'Temp/UnityLockfile')) { throw 'This isolated Unity project is already open. Do not alter a running test project.' }
New-Item -ItemType Directory -Force -Path $eventProject,$eventOutput | Out-Null
'Owned by tools/VoyageEventExpansion20261014/RunUnityValidation.ps1. Only Core and test code; no game artwork or player saves.' | Set-Content -LiteralPath $eventMarker -Encoding utf8
$eventCore=Join-Path $eventProject 'Assets/Core'
$eventTests=Join-Path $eventProject 'Assets/Tests/EditMode'
$eventPackages=Join-Path $eventProject 'Packages'
$eventSettings=Join-Path $eventProject 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $eventCore,$eventTests,$eventPackages,$eventSettings | Out-Null
$eventInputs=@(Get-ChildItem -LiteralPath (Join-Path $eventMain 'Assets/_Project/Core/Runtime') -File | Where-Object {$_.Extension -in '.cs','.asmdef'})
foreach($eventInput in $eventInputs) { Copy-Item -LiteralPath $eventInput.FullName -Destination $eventCore -Force }
$eventContent=Get-Item -LiteralPath (Join-Path $eventMain 'Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
Copy-Item -LiteralPath $eventContent.FullName -Destination $eventCore -Force
$eventInputs+=$eventContent
$eventFixtureNames=@('CoreVoyageCheckpointTests.cs','CoreSeaChartProgressTests.cs','CoreSeaChartEventPacingTests.cs','CoreSeaChartEventSerializationTests.cs','CoreVoyageEventConditionTests.cs','CoreVoyageEventCatalogTests.cs','VoyageEventDistributionTests.cs','ExpandedSeaGeometryTests.cs')
foreach($eventFixture in $eventFixtureNames) {
    $eventInput=Get-Item -LiteralPath (Join-Path $eventMain ('Assets/_Project/Tests/EditMode/'+$eventFixture))
    Copy-Item -LiteralPath $eventInput.FullName -Destination $eventTests -Force
    $eventInputs+=$eventInput
}
$eventJson=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'VoyageEventCheckpointJsonTests.cs')
Copy-Item -LiteralPath $eventJson.FullName -Destination $eventTests -Force
$eventInputs+=$eventJson
@{name='ShatteredSail.VoyageEventValidation.Tests';rootNamespace='ShatteredSail.Core.Tests';references=@('ShatteredSail.Core');includePlatforms=@('Editor');optionalUnityReferences=@('TestAssemblies');autoReferenced=$false} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $eventTests 'VoyageEventValidation.Tests.asmdef') -Encoding utf8
# Use already installed package code through local package references; avoid downloading or importing the main game's art/packages.
$eventPackageCache=Join-Path $eventMain 'Library/PackageCache'
$eventTestFramework=Get-ChildItem -LiteralPath $eventPackageCache -Directory -Filter 'com.unity.test-framework@*' | Select-Object -First 1
$eventNunit=Get-ChildItem -LiteralPath $eventPackageCache -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
if(-not $eventTestFramework -or -not $eventNunit) {throw 'Existing Unity test packages are required.'}
@{dependencies=@{'com.unity.test-framework'=('file:'+$eventTestFramework.FullName.Replace('\','/'));'com.unity.ext.nunit'=('file:'+$eventNunit.FullName.Replace('\','/'));'com.unity.modules.imgui'='1.0.0';'com.unity.modules.jsonserialize'='1.0.0'}} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $eventPackages 'manifest.json') -Encoding utf8
foreach($eventSetting in (Get-ChildItem -LiteralPath (Join-Path $eventMain 'ProjectSettings') -File)) {Copy-Item -LiteralPath $eventSetting.FullName -Destination $eventSettings -Force}
$eventProjectSettings=Join-Path $eventSettings 'ProjectSettings.asset'
$eventSettingsText=[IO.File]::ReadAllText($eventProjectSettings)
$eventSettingsText=[regex]::Replace($eventSettingsText,'(?m)^  companyName:.*$','  companyName: CodexValidation')
$eventSettingsText=[regex]::Replace($eventSettingsText,'(?m)^  productName:.*$','  productName: VoyageEventExpansion20261014')
[IO.File]::WriteAllText($eventProjectSettings,$eventSettingsText,[Text.UTF8Encoding]::new($false))
$eventInputs | ForEach-Object { [pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} } | Export-Csv -LiteralPath (Join-Path $eventOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$eventFixtureNames+'VoyageEventCheckpointJsonTests.cs' | Set-Content -LiteralPath (Join-Path $eventOutput 'fixtures.txt') -Encoding utf8
if($PrepareOnly){ Write-Output "Prepared isolated project: $eventProject";return }
$eventUnityArguments=@('-batchmode','-nographics','-projectPath',('"'+$eventProject+'"'),'-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $eventOutput 'results.xml')+'"'),'-logFile',('"'+(Join-Path $eventOutput 'editor.log')+'"'))
$eventUnityProcess=Start-Process -FilePath $UnityEditor -ArgumentList $eventUnityArguments -WindowStyle Hidden -PassThru -Wait
if($eventUnityProcess.ExitCode -ne 0){throw 'Isolated Unity JSON validation failed; inspect results.xml and editor.log.'}
$eventResult=Join-Path $eventOutput 'results.xml'
if(-not(Test-Path -LiteralPath $eventResult)){throw 'Unity exited without producing test results.'}
[xml]$eventXml=Get-Content -LiteralPath $eventResult
Write-Output ('Unity: '+$eventXml.'test-run'.result+' total='+$eventXml.'test-run'.total+' passed='+$eventXml.'test-run'.passed+' failed='+$eventXml.'test-run'.failed)
if($eventXml.'test-run'.failed -ne '0'){throw 'Unity tests reported failures.'}
