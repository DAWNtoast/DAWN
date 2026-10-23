param(
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [switch]$PrepareOnly,
    [switch]$SkipCompile
)
$ErrorActionPreference = 'Stop'
$tooltipWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tooltipMain = Join-Path $tooltipWorkspace 'UnityClient'
$tooltipProject = Join-Path $tooltipWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$tooltipOutput = Join-Path $tooltipWorkspace 'Artifacts/CardKeywordTooltip20261023'
$tooltipCompile = Join-Path $tooltipOutput 'Compile'
$tooltipMarker = Join-Path $tooltipProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'
if ((Test-Path -LiteralPath $tooltipProject) -and -not (Test-Path -LiteralPath $tooltipMarker)) {
    throw 'Refusing to reuse an unowned validation project.'
}
if (Test-Path -LiteralPath (Join-Path $tooltipProject 'Temp/UnityLockfile')) {
    throw 'The isolated tooltip validation project is already running.'
}
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }

$tooltipPlugins = Join-Path $tooltipProject 'Assets/Plugins'
$tooltipPlayTests = Join-Path $tooltipProject 'Assets/Tests/PlayMode'
$tooltipEditTests = Join-Path $tooltipProject 'Assets/Tests/Editor'
$tooltipPackages = Join-Path $tooltipProject 'Packages'
$tooltipSettings = Join-Path $tooltipProject 'ProjectSettings'
$tooltipFonts = Join-Path $tooltipProject 'Assets/Resources/Fonts'
New-Item -ItemType Directory -Force -Path $tooltipCompile,$tooltipPlugins,$tooltipPlayTests,$tooltipEditTests,$tooltipPackages,$tooltipSettings,$tooltipFonts | Out-Null
'Owned isolated card keyword tooltip validation; fresh runtime DLLs and focused tests. No game scenes, saved games, or retired UI previews.' |
    Set-Content -LiteralPath $tooltipMarker -Encoding utf8

if (-not $SkipCompile) {
    $tooltipUnityData = Join-Path (Split-Path -Parent $UnityEditor) 'Data'
    Push-Location $tooltipMain
    try {
        foreach ($tooltipAssembly in @('Core','Content','Persistence','Presentation')) {
            $tooltipResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($tooltipAssembly + '.rsp'))) |
                ForEach-Object { $_.Replace($ReferenceResponses.Replace('\','/'), $tooltipCompile.Replace('\','/')) }
            $tooltipSourcePattern = '^"?(?:D:/Game/UnityClient/)?Assets/_Project/' + $tooltipAssembly + '/Runtime/.*\.cs"?$'
            $tooltipResponse = @($tooltipResponse | Where-Object { $_ -notmatch $tooltipSourcePattern })
            $tooltipSources = Join-Path $tooltipMain ('Assets/_Project/' + $tooltipAssembly + '/Runtime')
            $tooltipResponse += @(Get-ChildItem -LiteralPath $tooltipSources -File -Filter '*.cs' -Recurse |
                Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
            $tooltipResponsePath = Join-Path $tooltipCompile ($tooltipAssembly + '.rsp')
            [IO.File]::WriteAllLines($tooltipResponsePath, $tooltipResponse, [Text.UTF8Encoding]::new($false))
            & (Join-Path $tooltipUnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $tooltipUnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $tooltipResponsePath) 2>&1 |
                Tee-Object -FilePath (Join-Path $tooltipCompile ($tooltipAssembly + '.log'))
            if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: ' + $tooltipAssembly) }
            Write-Output ('PASS ' + $tooltipAssembly)
        }
    } finally { Pop-Location }
}

$tooltipAssemblies = @('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Persistence.dll','ShatteredSail.Presentation.dll')
foreach ($tooltipAssembly in $tooltipAssemblies) {
    Copy-Item -LiteralPath (Join-Path $tooltipCompile $tooltipAssembly) -Destination $tooltipPlugins -Force
}
foreach ($tooltipFont in @('HarborBody-Regular.otf','HarborBody-Regular.otf.meta','XiangcuiKesong.ttf','XiangcuiKesong.ttf.meta')) {
    Copy-Item -LiteralPath (Join-Path $tooltipMain ('Assets/_Project/Resources/Fonts/' + $tooltipFont)) -Destination $tooltipFonts -Force
}
Copy-Item -LiteralPath (Join-Path $tooltipMain 'Assets/_Project/Tests/PlayMode/CardKeywordTooltipTests.cs') -Destination $tooltipPlayTests -Force
Copy-Item -LiteralPath (Join-Path $tooltipMain 'Assets/_Project/Tests/EditMode/CoreBattleCardKeywordTextTests.cs') -Destination $tooltipEditTests -Force
foreach ($tooltipTestMode in @('PlayMode','Editor')) {
    $tooltipAssemblyDefinition = @{
        name = 'ShatteredSail.CardKeyword.Validation.' + $tooltipTestMode
        references = @('Unity.UGUI','Unity.InputSystem')
        optionalUnityReferences = @('TestAssemblies')
        overrideReferences = $true
        precompiledReferences = $tooltipAssemblies + @('nunit.framework.dll')
        autoReferenced = $false
    }
    if ($tooltipTestMode -eq 'Editor') { $tooltipAssemblyDefinition.includePlatforms = @('Editor') }
    $tooltipAssemblyDefinition | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $tooltipProject ('Assets/Tests/' + $tooltipTestMode + '/CardKeyword.Validation.asmdef')) -Encoding utf8
}

$tooltipDependencies = [ordered]@{}
$tooltipMainManifest = Get-Content -LiteralPath (Join-Path $tooltipMain 'Packages/manifest.json') -Raw | ConvertFrom-Json
foreach ($tooltipDependency in $tooltipMainManifest.dependencies.PSObject.Properties) {
    if ($tooltipDependency.Name.StartsWith('com.unity.modules.')) {
        $tooltipDependencies[$tooltipDependency.Name] = $tooltipDependency.Value
    }
}
$tooltipPackageCache = Join-Path $tooltipMain 'Library/PackageCache'
foreach ($tooltipPackageName in @('com.unity.test-framework','com.unity.ext.nunit','com.unity.ugui','com.unity.inputsystem')) {
    $tooltipPackage = Get-ChildItem -LiteralPath $tooltipPackageCache -Directory -Filter ($tooltipPackageName + '@*') | Select-Object -First 1
    if (-not $tooltipPackage) { throw "Installed local Unity package is required: $tooltipPackageName" }
    $tooltipDependencies[$tooltipPackageName] = 'file:' + $tooltipPackage.FullName.Replace('\','/')
}
@{ dependencies = $tooltipDependencies } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $tooltipPackages 'manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $tooltipMain 'ProjectSettings/ProjectVersion.txt') -Destination $tooltipSettings -Force
Copy-Item -LiteralPath (Join-Path $tooltipMain 'ProjectSettings/ProjectSettings.asset') -Destination $tooltipSettings -Force
if ($PrepareOnly) { Write-Output ('Prepared ' + $tooltipProject); return }

foreach ($tooltipMode in @('EditMode','PlayMode')) {
    $tooltipResult = Join-Path $tooltipOutput ($tooltipMode + '.xml')
    $tooltipLog = Join-Path $tooltipOutput ($tooltipMode + '.log')
    $tooltipFilter = if ($tooltipMode -eq 'EditMode') { 'ShatteredSail.Core.Tests.CoreBattleCardKeywordTextTests' } else { 'ShatteredSail.Presentation.Tests.CardKeywordTooltipTests' }
    $tooltipArguments = @(
        '-batchmode','-nographics','-projectPath',('"' + $tooltipProject + '"'),
        '-runTests','-testPlatform',$tooltipMode,'-testFilter',$tooltipFilter,
        '-testResults',('"' + $tooltipResult + '"'),'-logFile',('"' + $tooltipLog + '"')
    )
    $tooltipStarted = [DateTime]::UtcNow
    $tooltipProcess = Start-Process -FilePath $UnityEditor -ArgumentList $tooltipArguments -WindowStyle Hidden -PassThru
    $tooltipProcess.WaitForExit()
    if ($tooltipProcess.ExitCode -ne 0) { throw "Unity validation failed. Inspect $tooltipLog" }
    if (-not (Test-Path -LiteralPath $tooltipResult)) { throw "Unity produced no $tooltipMode test result." }
    if ((Get-Item -LiteralPath $tooltipResult).LastWriteTimeUtc -lt $tooltipStarted.AddSeconds(-2)) { throw 'Unity did not replace the previous test result.' }
    [xml]$tooltipXml = Get-Content -LiteralPath $tooltipResult
    $tooltipRun = $tooltipXml.'test-run'
    Write-Output ("$tooltipMode Unity: " + $tooltipRun.result + ' total=' + $tooltipRun.total + ' passed=' + $tooltipRun.passed + ' failed=' + $tooltipRun.failed)
    if ($tooltipRun.result -ne 'Passed' -or [int]$tooltipRun.total -lt 1 -or [int]$tooltipRun.failed -ne 0) { throw "$tooltipMode keyword tests did not all pass." }
    Write-Output $tooltipResult
}
