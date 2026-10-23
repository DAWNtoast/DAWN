param(
    [string[]]$CatalogSources = @('ComponentArtCatalog.cs', 'ComponentVoyageContextResolver.cs')
)
$ErrorActionPreference = 'Stop'
function Write-NarrativeJson([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
}
$narrativeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$narrativeMain = Join-Path $narrativeWorkspace 'UnityClient'
$narrativeCompile = Join-Path $narrativeWorkspace 'Artifacts/NarrativeTextFix20261022/Compile'
$narrativeProject = Join-Path $narrativeWorkspace 'tmp/narrative-component-validation-20261022'
$narrativeMarker = Join-Path $narrativeProject 'OWNED_NARRATIVE_COMPONENT_VALIDATION.txt'
if ((Test-Path -LiteralPath $narrativeProject) -and -not (Test-Path -LiteralPath $narrativeMarker)) { throw 'Refusing to reuse an unowned validation project.' }
$narrativeRunning = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($narrativeProject.Replace('/','\'))
})
if ($narrativeRunning.Count) { throw 'The isolated validation project is running.' }
$narrativeLockPath = Join-Path $narrativeProject 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $narrativeLockPath) {
    # A failed editor startup can leave the file behind. Only reuse it when no
    # editor owns this project and an exclusive file handle can be acquired.
    $narrativeLock = [IO.File]::Open($narrativeLockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $narrativeLock.Dispose()
}
$narrativeCatalog = Join-Path $narrativeProject 'Assets/Catalog'
$narrativeTests = Join-Path $narrativeProject 'Assets/Tests/Editor'
$narrativePlugins = Join-Path $narrativeProject 'Assets/Plugins'
$narrativeResources = Join-Path $narrativeProject 'Assets/Resources/Data'
$narrativePackages = Join-Path $narrativeProject 'Packages'
$narrativeSettings = Join-Path $narrativeProject 'ProjectSettings'
$narrativeTestSource = Join-Path $narrativeMain 'Assets/_Project/Tests/EditMode/ComponentEnvironmentalEffectsTests.cs'
$narrativeCsvSource = Join-Path $narrativeMain 'Assets/_Project/Resources/Data/component_catalog_v2.csv'
$narrativePackageCache = Join-Path $narrativeMain 'Library/PackageCache'
$narrativeFramework = Get-ChildItem -LiteralPath $narrativePackageCache -Directory -Filter 'com.unity.test-framework@*' | Select-Object -First 1
$narrativeNunit = Get-ChildItem -LiteralPath $narrativePackageCache -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
if (-not $narrativeFramework -or -not $narrativeNunit) { throw 'Installed Unity test framework and NUnit packages are required.' }
foreach ($narrativeAssembly in @('Core', 'Content')) {
    $narrativeDll = Join-Path $narrativeCompile ('ShatteredSail.' + $narrativeAssembly + '.dll')
    if (-not (Test-Path -LiteralPath $narrativeDll -PathType Leaf)) { throw ('Run this task Compile.ps1 first: ' + $narrativeDll) }
    $narrativeDllTime = (Get-Item -LiteralPath $narrativeDll).LastWriteTimeUtc
    $narrativeNewerSources = @(Get-ChildItem -LiteralPath (Join-Path $narrativeMain ('Assets/_Project/' + $narrativeAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse |
        Where-Object { $_.LastWriteTimeUtc -gt $narrativeDllTime })
    if ($narrativeNewerSources.Count) { throw ('Production source changed after compilation; rerun Compile.ps1: ' + $narrativeNewerSources[0].FullName) }
}
foreach ($narrativeRequired in @($narrativeTestSource, $narrativeCsvSource)) {
    if (-not (Test-Path -LiteralPath $narrativeRequired -PathType Leaf)) { throw ('Missing validation input: ' + $narrativeRequired) }
}
foreach ($narrativeFile in $CatalogSources) {
    if ([IO.Path]::GetFileName($narrativeFile) -ne $narrativeFile -or -not $narrativeFile.EndsWith('.cs')) { throw ('Expected a Presentation runtime source filename: ' + $narrativeFile) }
    if (-not (Test-Path -LiteralPath (Join-Path $narrativeMain ('Assets/_Project/Presentation/Runtime/' + $narrativeFile)) -PathType Leaf)) { throw ('Missing catalog source: ' + $narrativeFile) }
}
New-Item -ItemType Directory -Force -Path $narrativeProject,$narrativeCatalog,$narrativeTests,$narrativePlugins,$narrativeResources,$narrativePackages,$narrativeSettings | Out-Null
'Owned minimal narrative/component validation. Only current catalog sources, Core/Content assemblies, the catalog CSV and focused tests. No scenes or art textures.' |
    Set-Content -LiteralPath $narrativeMarker -Encoding utf8
foreach ($narrativeFile in $CatalogSources) {
    Copy-Item -LiteralPath (Join-Path $narrativeMain ('Assets/_Project/Presentation/Runtime/' + $narrativeFile)) -Destination $narrativeCatalog -Force
}
foreach ($narrativeAssembly in @('Core', 'Content')) {
    Copy-Item -LiteralPath (Join-Path $narrativeCompile ('ShatteredSail.' + $narrativeAssembly + '.dll')) -Destination $narrativePlugins -Force
}
Copy-Item -LiteralPath $narrativeCsvSource -Destination $narrativeResources -Force
Copy-Item -LiteralPath $narrativeTestSource -Destination $narrativeTests -Force
Write-NarrativeJson (Join-Path $narrativeCatalog 'NarrativeComponent.Validation.asmdef') @{ name='ShatteredSail.NarrativeComponent.Validation'; references=@(); overrideReferences=$true; precompiledReferences=@('ShatteredSail.Core.dll','ShatteredSail.Content.dll'); autoReferenced=$false }
Write-NarrativeJson (Join-Path $narrativeTests 'NarrativeComponent.Validation.Tests.asmdef') @{ name='ShatteredSail.NarrativeComponent.Validation.Tests'; references=@('ShatteredSail.NarrativeComponent.Validation'); includePlatforms=@('Editor'); optionalUnityReferences=@('TestAssemblies'); overrideReferences=$true; precompiledReferences=@('ShatteredSail.Core.dll','ShatteredSail.Content.dll','nunit.framework.dll'); autoReferenced=$false }
Write-NarrativeJson (Join-Path $narrativePackages 'manifest.json') @{ dependencies=@{ 'com.unity.test-framework'=('file:'+$narrativeFramework.FullName.Replace('\','/')); 'com.unity.ext.nunit'=('file:'+$narrativeNunit.FullName.Replace('\','/')); 'com.unity.modules.imgui'='1.0.0'; 'com.unity.modules.jsonserialize'='1.0.0' } }
Copy-Item -LiteralPath (Join-Path $narrativeMain 'ProjectSettings/ProjectVersion.txt') -Destination $narrativeSettings -Force
$narrativePlayerSettings = [IO.File]::ReadAllText((Join-Path $narrativeMain 'ProjectSettings/ProjectSettings.asset'))
$narrativePlayerSettings = $narrativePlayerSettings -replace '(?m)^  companyName:.*$', '  companyName: NarrativeComponentValidation'
$narrativePlayerSettings = $narrativePlayerSettings -replace '(?m)^  productName:.*$', '  productName: NarrativeComponentValidation20261022'
$narrativePlayerSettings = $narrativePlayerSettings -replace '(?m)^    Standalone:.*$', '    Standalone: com.validation.narrativecomponent20261022'
[IO.File]::WriteAllText((Join-Path $narrativeSettings 'ProjectSettings.asset'), $narrativePlayerSettings, [Text.UTF8Encoding]::new($false))
# The art-availability test requires ship and component textures. This focused project
# deliberately contains only the CSV, so its filter excludes that unrelated test.
$narrativeTestText = [IO.File]::ReadAllText($narrativeTestSource)
$narrativeMethods = @([regex]::Matches($narrativeTestText, 'public\s+void\s+([A-Za-z_]\w*)\s*\(') |
    ForEach-Object { $_.Groups[1].Value } |
    Where-Object { $_ -ne 'ExpandedCatalog_AddsFiveContextAwareSidegradesPerSlot' } |
    Select-Object -Unique)
if (-not $narrativeMethods.Count) { throw 'No focused component environment test methods found.' }
$narrativeFilter = ($narrativeMethods | ForEach-Object { 'ShatteredSail.Core.Tests.ComponentEnvironmentalEffectsTests.' + $_ }) -join ';'
$narrativeValidation = [pscustomobject]@{
    ProjectPath = $narrativeProject
    Platform = 'EditMode'
    TestFilter = $narrativeFilter
    ExcludedTest = 'ExpandedCatalog_AddsFiveContextAwareSidegradesPerSlot (requires unrelated artwork)'
    CatalogSources = $CatalogSources
    PreparedUtc = [DateTime]::UtcNow.ToString('o')
}
Write-NarrativeJson (Join-Path $narrativeProject 'validation.json') $narrativeValidation
$narrativeValidation | ConvertTo-Json -Depth 4
Write-Output 'Prepared only; Unity was not started. Run tools/ReleaseBuild/RunTests.ps1 using this ProjectPath and TestFilter with a new Artifacts/NarrativeTextFix20261022 output directory.'
