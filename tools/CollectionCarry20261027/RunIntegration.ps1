param([int]$TimeoutSeconds = 240, [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'final')
$ErrorActionPreference = 'Stop'
$carryWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$carryProject = [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'tmp/collection-carry-20261027-integration'))
$carryOutput = Join-Path $carryWorkspace ('outputs/collection-carry-20261027/integration/' + $RunName)
$carrySource = Join-Path $carryWorkspace 'UnityClient'
if (Test-Path -LiteralPath $carryProject) { throw 'Use a fresh dedicated integration directory; inspect any existing one before cleanup.' }
if (Test-Path -LiteralPath $carryOutput) { throw 'Use a new RunName to retain earlier validation evidence.' }
foreach ($carryParent in @($carryWorkspace, (Join-Path $carryWorkspace 'tmp'))) {
    if ((Get-Item -LiteralPath $carryParent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked project parent is forbidden.' }
}
$carryProcess = $null
New-Item -ItemType Directory -Force -Path $carryOutput | Out-Null
try {
    foreach ($carryPart in @('Assets/Plugins', 'Assets/Tests', 'Assets/Resources/Data', 'Packages', 'ProjectSettings')) {
        New-Item -ItemType Directory -Force -Path (Join-Path $carryProject $carryPart) | Out-Null
    }
    'Task-owned, reproducible resource-free CollectionCarry20261027 integration project.' |
        Set-Content -LiteralPath (Join-Path $carryProject 'OWNED_COLLECTION_CARRY_VALIDATION.txt') -Encoding utf8
    $carryManifest = Get-Content -LiteralPath (Join-Path $carrySource 'Packages/manifest.json') -Raw | ConvertFrom-Json
    $carryDependencies = [ordered]@{}
    foreach ($carryDependency in $carryManifest.dependencies.PSObject.Properties) {
        if ($carryDependency.Name.StartsWith('com.unity.modules.')) { $carryDependencies[$carryDependency.Name] = $carryDependency.Value }
    }
    foreach ($carryPackage in @('com.unity.test-framework', 'com.unity.ext.nunit', 'com.unity.ugui', 'com.unity.inputsystem')) {
        $carryPackagePath = Get-ChildItem -LiteralPath (Join-Path $carrySource 'Library/PackageCache') -Directory -Filter ($carryPackage + '@*') | Select-Object -First 1
        if ($null -eq $carryPackagePath) { throw ('Missing local package: ' + $carryPackage) }
        $carryDependencies[$carryPackage] = 'file:' + $carryPackagePath.FullName.Replace('\', '/')
    }
    @{ dependencies=$carryDependencies } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $carryProject 'Packages/manifest.json') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $carryProject 'Packages/manifest.json') -Destination (Join-Path $carryOutput 'manifest.json') -Force
    Copy-Item -LiteralPath (Join-Path $carrySource 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $carryProject 'ProjectSettings/ProjectVersion.txt')
    $carrySettings = [IO.File]::ReadAllText((Join-Path $carrySource 'ProjectSettings/ProjectSettings.asset'))
    $carrySettings = [regex]::Replace($carrySettings, '(?m)^  companyName:.*$', '  companyName: CodexCollectionCarryValidation')
    $carrySettings = [regex]::Replace($carrySettings, '(?m)^  productName:.*$', ('  productName: CollectionCarry20261027_' + [Guid]::NewGuid().ToString('N')))
    [IO.File]::WriteAllText((Join-Path $carryProject 'ProjectSettings/ProjectSettings.asset'), $carrySettings, [Text.UTF8Encoding]::new($false))
    $carryAssembly = @{
        name='CollectionCarryValidation.PlayMode'; references=@('Unity.UGUI','Unity.InputSystem');
        autoReferenced=$false; overrideReferences=$true;
        precompiledReferences=@('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Persistence.dll','ShatteredSail.Presentation.dll','nunit.framework.dll');
        optionalUnityReferences=@('TestAssemblies')
    }
    $carryAssembly | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $carryProject 'Assets/Tests/CollectionCarryValidation.PlayMode.asmdef') -Encoding utf8
    $carryTestFiles = @(
        (Join-Path $PSScriptRoot 'CollectionCarryIntegrationTests.cs'),
        (Join-Path $carrySource 'Assets/_Project/Tests/PlayMode/CollectionIncomeFlowTests.cs'),
        (Join-Path $carrySource 'Assets/_Project/Tests/PlayMode/NavalBattleVisualCapture.cs')
    )
    foreach ($carryTest in $carryTestFiles) { Copy-Item -LiteralPath $carryTest -Destination (Join-Path $carryProject 'Assets/Tests') }
    # Full SaveProgress scans component-purchase achievements. It needs this data
    # catalog, but none of the component textures or any game scene.
    $carryComponentCatalog = Join-Path $carrySource 'Assets/_Project/Resources/Data/component_catalog_v2.csv'
    Copy-Item -LiteralPath $carryComponentCatalog -Destination (Join-Path $carryProject 'Assets/Resources/Data')
    $carryTestFiles += $carryComponentCatalog
    $carryTestFiles | ForEach-Object { [pscustomobject]@{Source=$_; SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} } |
        Export-Csv -LiteralPath (Join-Path $carryOutput 'test-source-hashes.csv') -NoTypeInformation -Encoding utf8
    & (Join-Path $PSScriptRoot 'CompileProduction.ps1') -StageIntegration
    $carryFilters = @('CollectionCarryValidation.CollectionCarryIntegrationTests')
    $carryIncomeSource = [IO.File]::ReadAllText($carryTestFiles[1])
    foreach ($carryMatch in [regex]::Matches($carryIncomeSource, '\[Test\]\s*public\s+void\s+([A-Za-z0-9_]+)\s*\(')) {
        $carryFilters += 'ShatteredSail.Presentation.Tests.CollectionIncomeFlowTests.' + $carryMatch.Groups[1].Value
    }
    $carryFilters | Set-Content -LiteralPath (Join-Path $carryOutput 'test-filter.txt') -Encoding utf8
    $carryArgs = @('-batchmode','-nographics','-projectPath',$carryProject,'-runTests','-testPlatform','PlayMode',
        '-testFilter',($carryFilters -join ';'),'-testResults',(Join-Path $carryOutput 'results.xml'),'-logFile',(Join-Path $carryOutput 'editor.log'))
    $carryQuotedArgs = @($carryArgs | ForEach-Object { '"' + $_ + '"' })
    $carryProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $carryQuotedArgs -WindowStyle Hidden -PassThru
    [pscustomobject]@{ProcessId=$carryProcess.Id; Project=$carryProject; Arguments=$carryArgs} | ConvertTo-Json -Depth 3 |
        Set-Content -LiteralPath (Join-Path $carryOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity resource-free PlayMode integration PID ' + $carryProcess.Id)
    $carryTimer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $carryProcess.WaitForExit(1000)) {
        if ($carryTimer.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
            Stop-Process -Id $carryProcess.Id -Force
            $carryProcess.WaitForExit()
            throw 'Bounded integration attempt timed out; inspect preserved editor.log.'
        }
    }
    $carryProcess.Refresh()
    if ($carryProcess.ExitCode -ne 0) { throw ('Unity integration failed with exit ' + $carryProcess.ExitCode + '; inspect editor.log.') }
    if (-not (Test-Path -LiteralPath (Join-Path $carryOutput 'results.xml'))) { throw 'Unity returned without test results.' }
    [xml]$carryResults = Get-Content -LiteralPath (Join-Path $carryOutput 'results.xml')
    $carryRun = $carryResults.'test-run'
    Write-Output ('PlayMode: ' + $carryRun.result + ' passed=' + $carryRun.passed + ' failed=' + $carryRun.failed)
    if ($carryRun.result -ne 'Passed' -or [int]$carryRun.passed -lt 1) { throw 'PlayMode integration test failures.' }
} finally {
    if ($null -ne $carryProcess -and -not $carryProcess.HasExited) { throw 'Task Unity still running; preserve project until stopped.' }
    if (Test-Path -LiteralPath $carryProject) {
        if ($carryProject -ne [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'tmp/collection-carry-20261027-integration')) -or
            -not (Test-Path -LiteralPath (Join-Path $carryProject 'OWNED_COLLECTION_CARRY_VALIDATION.txt'))) { throw 'Unexpected cleanup project.' }
        $carryAncestor = Get-Item -LiteralPath $carryProject -Force
        while ($null -ne $carryAncestor) {
            if ($carryAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked cleanup ancestor is forbidden.' }
            $carryAncestor = $carryAncestor.Parent
        }
        $carryItems = @(Get-ChildItem -LiteralPath $carryProject -Recurse -Force)
        if (@($carryItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) { throw 'Linked project contents found; cleanup deferred.' }
        $carryConsumers = @(Get-CimInstance Win32_Process | Where-Object {
            $_.ProcessId -ne $PID -and $_.CommandLine -and $_.Name -in @('Unity.exe','mono.exe','dotnet.exe','UnityShaderCompiler.exe','bee_backend.exe') -and
            $_.CommandLine.Replace('/', '\').IndexOf($carryProject.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0
        })
        if ($carryConsumers.Count -gt 0) { throw 'Another process references this project; cleanup deferred.' }
        $carryReleased = [long](($carryItems | Where-Object { -not $_.PSIsContainer } | Measure-Object -Property Length -Sum).Sum)
        Remove-Item -LiteralPath $carryProject -Recurse -Force
        if (Test-Path -LiteralPath $carryProject) { throw 'Task integration project cleanup failed.' }
        [pscustomobject]@{ReleasedBytes=$carryReleased; ProjectRemaining=$false; MainProjectCleaned=$false; EvidenceRetained=$true; LocalPackageReferencesRetained=$true} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $carryOutput 'cleanup.json') -Encoding utf8
        Write-Output ('Cleaned dedicated integration project and caches: ' + $carryReleased + ' bytes.')
    }
}
