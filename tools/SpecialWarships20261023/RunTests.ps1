param(
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$shipWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$shipMain = Join-Path $shipWorkspace 'UnityClient'
$shipProject = Join-Path $shipWorkspace 'tmp/special-warships-20261023-validation'
$shipOutput = Join-Path $shipWorkspace 'Artifacts/SpecialWarships20261023/Tests'
$shipMarker = Join-Path $shipProject 'OWNED_SPECIAL_WARSHIP_VALIDATION.txt'
if ((Test-Path -LiteralPath $shipProject) -and -not (Test-Path -LiteralPath $shipMarker)) {
    throw 'Refusing to reuse an unowned validation project.'
}
if (Test-Path -LiteralPath (Join-Path $shipProject 'Temp/UnityLockfile')) {
    throw 'The isolated special-warship validation project is running.'
}
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }

$shipKeys = @(
    'boss_fire', 'boss_tide', 'boss_blood', 'boss_thunder', 'boss_curse', 'boss_arcane',
    'legendary_fire', 'legendary_tide', 'legendary_blood', 'legendary_thunder', 'legendary_curse', 'legendary_arcane'
)
$shipSourceResources = Join-Path $shipMain 'Assets/_Project/Resources/ShipArtV2/Special'
foreach ($shipKey in $shipKeys) {
    foreach ($shipSuffix in @('.png', '.png.meta')) {
        $shipSource = Join-Path $shipSourceResources ($shipKey + $shipSuffix)
        if (-not (Test-Path -LiteralPath $shipSource)) { throw "Missing production asset: $shipSource" }
    }
}

# Rebuild the actual runtime assemblies; never validate against a stale imported Core or Presentation DLL.
$shipCompileStarted = [DateTime]::UtcNow
& (Join-Path $shipWorkspace 'tools/EventIllustrations20261022/Compile.ps1')
$shipCompileRoot = Join-Path $shipWorkspace 'Artifacts/EventIllustrations20261022/Compile'
$shipAssemblyNames = @('ShatteredSail.Core.dll', 'ShatteredSail.Content.dll', 'ShatteredSail.Persistence.dll', 'ShatteredSail.Presentation.dll')
foreach ($shipAssemblyName in $shipAssemblyNames) {
    $shipAssembly = Get-Item -LiteralPath (Join-Path $shipCompileRoot $shipAssemblyName)
    if ($shipAssembly.LastWriteTimeUtc -lt $shipCompileStarted.AddSeconds(-2)) {
        throw "Runtime assembly was not freshly compiled: $shipAssemblyName"
    }
}

$shipTests = Join-Path $shipProject 'Assets/Tests/Editor'
$shipPlugins = Join-Path $shipProject 'Assets/Plugins'
$shipResources = Join-Path $shipProject 'Assets/Resources/ShipArtV2/Special'
$shipPackages = Join-Path $shipProject 'Packages'
$shipSettings = Join-Path $shipProject 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $shipProject,$shipOutput,$shipTests,$shipPlugins,$shipResources,$shipPackages,$shipSettings | Out-Null
'Owned isolated special-warship validation: twelve production PNGs, their metadata, fresh runtime DLLs and the existing five tests. No game scenes or saved games.' |
    Set-Content -LiteralPath $shipMarker -Encoding utf8
foreach ($shipKey in $shipKeys) {
    foreach ($shipSuffix in @('.png', '.png.meta')) {
        Copy-Item -LiteralPath (Join-Path $shipSourceResources ($shipKey + $shipSuffix)) -Destination $shipResources -Force
    }
}
foreach ($shipAssemblyName in $shipAssemblyNames) {
    Copy-Item -LiteralPath (Join-Path $shipCompileRoot $shipAssemblyName) -Destination $shipPlugins -Force
}
Copy-Item -LiteralPath (Join-Path $shipMain 'Assets/_Project/Tests/EditMode/SpecialWarshipArtCatalogTests.cs') -Destination $shipTests -Force
@{
    name = 'ShatteredSail.SpecialWarship.Validation.Tests'
    references = @('Unity.UGUI', 'Unity.InputSystem')
    includePlatforms = @('Editor')
    optionalUnityReferences = @('TestAssemblies')
    overrideReferences = $true
    precompiledReferences = @($shipAssemblyNames) + @('nunit.framework.dll')
    autoReferenced = $false
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $shipTests 'SpecialWarship.Validation.Tests.asmdef') -Encoding utf8

# The four package implementations are already installed in the main project.
# Engine modules are built into this Unity installation and do not require registry downloads.
$shipDependencies = [ordered]@{}
$shipMainManifest = Get-Content -LiteralPath (Join-Path $shipMain 'Packages/manifest.json') -Raw | ConvertFrom-Json
foreach ($shipDependency in $shipMainManifest.dependencies.PSObject.Properties) {
    if ($shipDependency.Name.StartsWith('com.unity.modules.')) {
        $shipDependencies[$shipDependency.Name] = $shipDependency.Value
    }
}
$shipPackageCache = Join-Path $shipMain 'Library/PackageCache'
foreach ($shipPackageName in @('com.unity.test-framework', 'com.unity.ext.nunit', 'com.unity.ugui', 'com.unity.inputsystem')) {
    $shipPackage = Get-ChildItem -LiteralPath $shipPackageCache -Directory -Filter ($shipPackageName + '@*') | Select-Object -First 1
    if (-not $shipPackage) { throw "Installed local Unity package is required: $shipPackageName" }
    $shipDependencies[$shipPackageName] = 'file:' + $shipPackage.FullName.Replace('\', '/')
}
@{ dependencies = $shipDependencies } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $shipPackages 'manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $shipMain 'ProjectSettings/ProjectVersion.txt') -Destination $shipSettings -Force

$shipResult = Join-Path $shipOutput 'results.xml'
$shipLog = Join-Path $shipOutput 'editor.log'
$shipFilter = 'ShatteredSail.Core.Tests.SpecialWarshipArtCatalogTests'
$shipArguments = @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $shipProject + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-testFilter', ('"' + $shipFilter + '"'),
    '-testResults', ('"' + $shipResult + '"'), '-logFile', ('"' + $shipLog + '"')
)
$shipTestStarted = [DateTime]::UtcNow
$shipProcess = Start-Process -FilePath $UnityEditor -ArgumentList $shipArguments -WindowStyle Hidden -PassThru -Wait
if ($shipProcess.ExitCode -ne 0) { throw "Unity validation failed. Inspect $shipLog" }
if (-not (Test-Path -LiteralPath $shipResult)) { throw 'Unity produced no test result.' }
if ((Get-Item -LiteralPath $shipResult).LastWriteTimeUtc -lt $shipTestStarted.AddSeconds(-2)) {
    throw 'Unity did not replace the previous test result.'
}
[xml]$shipXml = Get-Content -LiteralPath $shipResult
$shipRun = $shipXml.'test-run'
Write-Output ("Unity: " + $shipRun.result + " total=" + $shipRun.total + " passed=" + $shipRun.passed + " failed=" + $shipRun.failed)
if ($shipRun.result -ne 'Passed' -or [int]$shipRun.total -ne 5 -or [int]$shipRun.passed -ne 5 -or [int]$shipRun.failed -ne 0) {
    throw 'Expected all five existing special-warship tests to pass.'
}
Write-Output $shipResult
