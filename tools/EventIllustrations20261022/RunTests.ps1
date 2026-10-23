param(
    [switch]$IncludeResourceValidation,
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$eventWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$eventMain = Join-Path $eventWorkspace 'UnityClient'
$eventProject = Join-Path $eventWorkspace 'tmp/event-illustrations-20261022-validation'
$eventOutput = Join-Path $eventWorkspace 'Artifacts/EventIllustrations20261022/Tests'
$eventMarker = Join-Path $eventProject 'OWNED_EVENT_ILLUSTRATION_VALIDATION.txt'
if ((Test-Path -LiteralPath $eventProject) -and -not (Test-Path -LiteralPath $eventMarker)) { throw 'Refusing to reuse an unowned validation project.' }
if (Test-Path -LiteralPath (Join-Path $eventProject 'Temp/UnityLockfile')) { throw 'The isolated validation project is running.' }
$eventCatalogDirectory = Join-Path $eventProject 'Assets/Catalog'
$eventTestDirectory = Join-Path $eventProject 'Assets/Tests/Editor'
$eventPluginDirectory = Join-Path $eventProject 'Assets/Plugins'
$eventPackagesDirectory = Join-Path $eventProject 'Packages'
$eventSettingsDirectory = Join-Path $eventProject 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $eventProject,$eventOutput,$eventCatalogDirectory,$eventTestDirectory,$eventPluginDirectory,$eventPackagesDirectory,$eventSettingsDirectory | Out-Null
'Owned isolated event illustration validation; only event artwork copied when resource validation is requested. No game scenes or saved games.' | Set-Content -LiteralPath $eventMarker -Encoding utf8
if ($IncludeResourceValidation) {
    $eventResourceDirectory = Join-Path $eventProject 'Assets/Resources/EncounterIllustrations'
    New-Item -ItemType Directory -Path $eventResourceDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $eventMain 'Assets/_Project/Resources/EncounterIllustrations') -File |
        Copy-Item -Destination $eventResourceDirectory -Force
    foreach ($eventSubdirectory in @('BossScenes', 'Reviewed20261023')) {
        $eventSubSource = Join-Path $eventMain ('Assets/_Project/Resources/EncounterIllustrations/' + $eventSubdirectory)
        if (Test-Path -LiteralPath $eventSubSource) {
            $eventSubDestination = Join-Path $eventResourceDirectory $eventSubdirectory
            New-Item -ItemType Directory -Path $eventSubDestination -Force | Out-Null
            Get-ChildItem -LiteralPath $eventSubSource -File | Copy-Item -Destination $eventSubDestination -Force
        }
    }
}
foreach ($eventFile in @('EncounterIllustrationCatalog.cs','LegacyDemoArtCatalog.cs')) {
    Copy-Item -LiteralPath (Join-Path $eventMain ('Assets/_Project/Presentation/Runtime/' + $eventFile)) -Destination $eventCatalogDirectory -Force
}
# Prefer the fresh full-source compilation; fall back to the imported assembly for isolated checks.
$eventCoreAssembly = Join-Path $eventWorkspace 'Artifacts/EventIllustrations20261022/Compile/ShatteredSail.Core.dll'
if (-not (Test-Path -LiteralPath $eventCoreAssembly)) { $eventCoreAssembly = Join-Path $eventMain 'Library/ScriptAssemblies/ShatteredSail.Core.dll' }
Copy-Item -LiteralPath $eventCoreAssembly -Destination $eventPluginDirectory -Force
$eventTestSource = Join-Path $eventMain 'Assets/_Project/Tests/EditMode/EncounterIllustrationCatalogTests.cs'
$eventTestText = [IO.File]::ReadAllText($eventTestSource)
# File-contract validation checks the production resource tree without importing all large textures into the small test project.
$eventTestText = $eventTestText.Replace('Application.dataPath', ('@"' + (Join-Path $eventMain 'Assets').Replace('"','""') + '"'))
$eventAuditOutput = Join-Path $eventWorkspace 'outputs/event-consistency-audit-20261023'
$eventTestText = $eventTestText.Replace('TestContext.CurrentContext.WorkDirectory', ('@"' + $eventAuditOutput.Replace('"','""') + '"'))
[IO.File]::WriteAllText((Join-Path $eventTestDirectory 'EncounterIllustrationCatalogTests.cs'), $eventTestText, [Text.UTF8Encoding]::new($false))
@{ name='ShatteredSail.EventArt.Validation'; references=@(); overrideReferences=$true; precompiledReferences=@('ShatteredSail.Core.dll'); autoReferenced=$false } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $eventCatalogDirectory 'EventArt.Validation.asmdef') -Encoding utf8
@{ name='ShatteredSail.EventArt.Validation.Tests'; references=@('ShatteredSail.EventArt.Validation'); includePlatforms=@('Editor'); optionalUnityReferences=@('TestAssemblies'); overrideReferences=$true; precompiledReferences=@('ShatteredSail.Core.dll','nunit.framework.dll'); autoReferenced=$false } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $eventTestDirectory 'EventArt.Validation.Tests.asmdef') -Encoding utf8
$eventPackageCache = Join-Path $eventMain 'Library/PackageCache'
$eventTestFramework = Get-ChildItem -LiteralPath $eventPackageCache -Directory -Filter 'com.unity.test-framework@*' | Select-Object -First 1
$eventNunit = Get-ChildItem -LiteralPath $eventPackageCache -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
if (-not $eventTestFramework -or -not $eventNunit) { throw 'Installed Unity test packages are required.' }
@{ dependencies=@{ 'com.unity.test-framework'=('file:'+$eventTestFramework.FullName.Replace('\','/')); 'com.unity.ext.nunit'=('file:'+$eventNunit.FullName.Replace('\','/')); 'com.unity.modules.imgui'='1.0.0'; 'com.unity.modules.jsonserialize'='1.0.0' } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $eventPackagesDirectory 'manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $eventMain 'ProjectSettings/ProjectVersion.txt') -Destination $eventSettingsDirectory -Force
$eventFilter = 'ShatteredSail.Core.Tests.EncounterIllustrationCatalogTests'
if (-not $IncludeResourceValidation) {
    $eventMethods = @('EveryExistingVoyageAndTreasureEventHasAnAuthoredNewIllustration','EveryRuntimeStoryVariantHasAnAuthoredIllustration','ReviewedPaintingsMatchAllTwentyNineActualStoryScenes','ReviewedSeaScenesDoNotReplaceUnrelatedTreasureMirrorOrGuidedBuoy','TreasureCoreVariantsKeepOldMapsOnTheOriginalStoneDoorScene','ActualEncounterLoadingKeepsSharedIdVariantsDistinctEvenWhenArtIsMissing','SpoiledProvisionsShareTheAuthoredHoldScene','TreasureAliasesDepictTheActivityInsteadOfUnrelatedOldCells','GuidedAndStandaloneTutorialsUseTheirOwnSharedIdMeanings','SharedImagesLoadOnceAndReleaseOnlyBorrowedReferences','MissingAndUnknownImagesNeverLoadAnUnrelatedHashedScene')
    $eventFilter = ($eventMethods | ForEach-Object { 'ShatteredSail.Core.Tests.EncounterIllustrationCatalogTests.' + $_ }) -join ';'
}
$eventArguments = @('-batchmode','-nographics','-projectPath',('"'+$eventProject+'"'),'-runTests','-testPlatform','EditMode','-testFilter',('"'+$eventFilter+'"'),'-testResults',('"'+(Join-Path $eventOutput 'results.xml')+'"'),'-logFile',('"'+(Join-Path $eventOutput 'editor.log')+'"'))
$eventTestStarted = [DateTime]::UtcNow
$eventProcess = Start-Process -FilePath $UnityEditor -ArgumentList $eventArguments -WindowStyle Hidden -PassThru
# Shared compiler servers can outlive Unity. Only wait for this Editor process.
$eventProcess.WaitForExit()
if ($eventProcess.ExitCode -ne 0) { throw 'Unity validation failed. Inspect Artifacts/EventIllustrations20261022/Tests/editor.log.' }
$eventResult = Join-Path $eventOutput 'results.xml'
if (-not (Test-Path -LiteralPath $eventResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $eventResult).LastWriteTimeUtc -lt $eventTestStarted.AddSeconds(-2)) { throw 'Unity did not replace the previous test result.' }
[xml]$eventXml = Get-Content -LiteralPath $eventResult
Write-Output ('Unity: ' + $eventXml.'test-run'.result + ' total=' + $eventXml.'test-run'.total + ' passed=' + $eventXml.'test-run'.passed + ' failed=' + $eventXml.'test-run'.failed)
if ($eventXml.'test-run'.failed -ne '0' -or $eventXml.'test-run'.total -eq '0') { throw 'Unity reported failed or empty tests.' }
