param(
    [string]$ProjectPath = 'D:/Game/tmp/gm-tools-20261023-validation',
    [switch]$RefreshSources
)
$ErrorActionPreference = 'Stop'
$gmWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$gmSource = Join-Path $gmWorkspace 'UnityClient'
$gmProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$gmTemporaryRoot = (Join-Path $gmWorkspace 'tmp') + [IO.Path]::DirectorySeparatorChar
if (-not $gmProject.StartsWith($gmTemporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not ([IO.Path]::GetFileName($gmProject)).StartsWith('gm-tools-20261023-', [StringComparison]::Ordinal)) {
    throw 'Use a task-specific gm-tools-20261023-* directory inside workspace tmp.'
}
$gmMarker = Join-Path $gmProject 'OWNED_GM_TOOLS_VALIDATION.txt'
if (Test-Path -LiteralPath $gmProject) {
    if (-not $RefreshSources -or -not (Test-Path -LiteralPath $gmMarker)) { throw 'Choose a new project, or explicitly refresh this task-owned validation project.' }
} elseif ($RefreshSources) { throw 'The task-owned validation project does not exist.' }
if (Test-Path -LiteralPath (Join-Path $gmProject 'Temp/UnityLockfile')) { throw 'Validation project is running.' }
foreach ($gmRunning in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
    if ($gmRunning.CommandLine -and $gmRunning.CommandLine.Replace('/','\').Contains($gmProject.Replace('/','\'))) {
        throw 'Validation project is running.'
    }
}
[IO.Directory]::CreateDirectory($gmProject) | Out-Null
if (-not $RefreshSources) {
    'Owned by GM tools 20261023 validation. Sources, two targeted fixtures, component CSV and fonts only; no scenes, editor generators or shared cache.' |
        Set-Content -LiteralPath $gmMarker -Encoding utf8
}
$gmHashes = [Collections.Generic.List[object]]::new()
function Copy-GmInput([string]$Relative) {
    $gmInput = Join-Path $gmSource $Relative
    $gmDestination = Join-Path $gmProject $Relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($gmDestination)) | Out-Null
    $gmHash = (Get-FileHash -LiteralPath $gmInput -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $gmInput -Destination $gmDestination -Force
    if ((Get-FileHash -LiteralPath $gmDestination -Algorithm SHA256).Hash -ne $gmHash) { throw ('Copied input changed: ' + $Relative) }
    $gmHashes.Add([pscustomobject]@{Relative=$Relative; Source=$gmInput; Destination=$gmDestination; SHA256=$gmHash})
}
foreach ($gmAssembly in @('Core','Content','Persistence','Presentation')) {
    $gmRelativeRoot = 'Assets/_Project/' + $gmAssembly + '/Runtime'
    foreach ($gmFile in Get-ChildItem -LiteralPath (Join-Path $gmSource $gmRelativeRoot) -Recurse -File |
        Where-Object { $_.Name -match '\.(cs|asmdef)(\.meta)?$' }) {
        Copy-GmInput $gmFile.FullName.Substring($gmSource.Length + 1)
    }
}
foreach ($gmFixture in @(
    'EditMode/CoreGameMasterToolsTests.cs', 'EditMode/CoreGameMasterToolsTests.cs.meta',
    'EditMode/ShatteredSail.Core.Tests.asmdef', 'EditMode/ShatteredSail.Core.Tests.asmdef.meta',
    'PlayMode/GameMasterRuntimeTests.cs', 'PlayMode/GameMasterRuntimeTests.cs.meta',
    'PlayMode/NavalBattleVisualCapture.cs', 'PlayMode/NavalBattleVisualCapture.cs.meta',
    'PlayMode/ShatteredSail.Presentation.PlayModeTests.asmdef', 'PlayMode/ShatteredSail.Presentation.PlayModeTests.asmdef.meta')) {
    Copy-GmInput ('Assets/_Project/Tests/' + $gmFixture)
}
if (-not $RefreshSources) {
    foreach ($gmResource in @(
        'Data/component_catalog_v2.csv', 'Data/component_catalog_v2.csv.meta',
        'Fonts/XiangcuiKesong.ttf', 'Fonts/XiangcuiKesong.ttf.meta',
        'Fonts/HarborBody-Regular.otf', 'Fonts/HarborBody-Regular.otf.meta')) {
        Copy-GmInput ('Assets/_Project/Resources/' + $gmResource)
    }
    foreach ($gmSetting in @('ProjectVersion.txt','ProjectSettings.asset','InputManager.asset','TagManager.asset','TimeManager.asset')) {
        Copy-GmInput ('ProjectSettings/' + $gmSetting)
    }
    $gmSettingsPath = Join-Path $gmProject 'ProjectSettings/ProjectSettings.asset'
    $gmSettings = [IO.File]::ReadAllText($gmSettingsPath)
    $gmSettings = [regex]::Replace($gmSettings, '(?m)^  companyName:.*$', '  companyName: ShatteredSailValidation')
    $gmSettings = [regex]::Replace($gmSettings, '(?m)^  productName:.*$', '  productName: GmTools20261023Validation')
    [IO.File]::WriteAllText($gmSettingsPath, $gmSettings, [Text.UTF8Encoding]::new($false))
    $gmDependencies = [ordered]@{}
    $gmMainManifest = Get-Content -LiteralPath (Join-Path $gmSource 'Packages/manifest.json') -Raw | ConvertFrom-Json
    foreach ($gmDependency in $gmMainManifest.dependencies.PSObject.Properties) {
        if ($gmDependency.Name.StartsWith('com.unity.modules.', [StringComparison]::Ordinal)) { $gmDependencies[$gmDependency.Name] = $gmDependency.Value }
    }
    foreach ($gmPackage in @('com.unity.inputsystem','com.unity.ugui','com.unity.test-framework','com.unity.ext.nunit')) {
        $gmCache = Get-ChildItem -LiteralPath (Join-Path $gmSource 'Library/PackageCache') -Directory -Filter ($gmPackage + '@*') | Select-Object -First 1
        if (-not $gmCache) { throw ('Installed package missing: ' + $gmPackage) }
        $gmDependencies[$gmPackage] = 'file:' + $gmCache.FullName.Replace('\','/')
    }
    [IO.Directory]::CreateDirectory((Join-Path $gmProject 'Packages')) | Out-Null
    @{ dependencies=$gmDependencies } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $gmProject 'Packages/manifest.json') -Encoding utf8
}
$gmHashes | Export-Csv -LiteralPath (Join-Path $gmProject 'source-inputs.csv') -NoTypeInformation -Encoding utf8
if (Test-Path -LiteralPath (Join-Path $gmProject 'Assets/_Project/Scenes/UIPreviewsForMe')) { throw 'Retired preview directory must not exist in this minimal validation project.' }
[pscustomobject]@{Project=$gmProject;CopiedInputs=$gmHashes.Count;StorageIdentity='ShatteredSailValidation/GmTools20261023Validation';Fixtures=@('CoreGameMasterToolsTests','GameMasterRuntimeTests');Scope='Logic and real GM controls in isolated canvas; minimal resources with optional GM panel capture, no game scene validation.'} | ConvertTo-Json -Depth 4
