param(
    [string[]]$Fixtures = @('CoreSeaChartProgressTests', 'CoreVoyageSessionTests',
        'EncounterDiscoveryRulesTests', 'CoreGuidedTutorialVoyageTests', 'CoreGuidedBossScriptTests')
)
$ErrorActionPreference = 'Stop'
$chartWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$chartOutput = [IO.Path]::GetFullPath((Join-Path $chartWorkspace 'outputs/sea-chart-threshold-20261028/core-tests'))
$chartBuild = Join-Path $chartOutput 'Build'
$chartUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$chartFramework = Join-Path $chartUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$chartNunit = Get-ChildItem -LiteralPath (Join-Path $chartWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$chartTests = Join-Path $chartWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$chartFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $chartTests ($_ + '.cs')) })
$chartSources = @(Get-ChildItem -LiteralPath (Join-Path $chartWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$chartSources += $chartFixtures
$chartSources += Get-Item -LiteralPath (Join-Path $chartWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$chartSources += Get-Item -LiteralPath (Join-Path $chartWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
function Assert-ChartBuildDirectory {
    $chartExpected = [IO.Path]::GetFullPath((Join-Path $chartWorkspace 'outputs/sea-chart-threshold-20261028/core-tests/Build'))
    if ([IO.Path]::GetFullPath($chartBuild) -ne $chartExpected) { throw 'Unexpected sea-chart test build directory.' }
    foreach ($chartDirectory in @($chartWorkspace, (Join-Path $chartWorkspace 'outputs'),
        (Join-Path $chartWorkspace 'outputs/sea-chart-threshold-20261028'), $chartOutput, $chartBuild)) {
        if ((Test-Path -LiteralPath $chartDirectory) -and
            ((Get-Item -LiteralPath $chartDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked test directory: ' + $chartDirectory)
        }
    }
}
Assert-ChartBuildDirectory
try {
New-Item -ItemType Directory -Force -Path $chartBuild | Out-Null
$chartHashes = @($chartSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$chartHashes | Export-Csv -LiteralPath (Join-Path $chartOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $chartNunit -Destination (Join-Path $chartBuild 'nunit.framework.dll') -Force
$chartFixtures.Name | Set-Content -LiteralPath (Join-Path $chartOutput 'fixtures.txt') -Encoding utf8
$chartExe = Join-Path $chartBuild 'SeaChartTests.exe'
$chartArgs = @((Join-Path $chartUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$chartExe", "-r:$chartNunit")
foreach ($chartAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $chartArgs += '-r:' + (Join-Path $chartFramework $chartAssembly)
}
$chartArgs += $chartSources.FullName
& (Join-Path $chartUnity 'NetCoreRuntime/dotnet.exe') @chartArgs 2>&1 | Tee-Object -FilePath (Join-Path $chartOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Sea-chart Core compilation failed.' }
& (Join-Path $chartUnity 'MonoBleedingEdge/bin/mono.exe') $chartExe $chartOutput 2>&1 | Tee-Object -FilePath (Join-Path $chartOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Sea-chart Core tests failed; see results.xml and failures.txt.' }
foreach ($chartHash in $chartHashes) {
    if ((Get-FileHash -LiteralPath $chartHash.Source -Algorithm SHA256).Hash -ne $chartHash.SHA256) {
        throw ('Source changed during tests: ' + $chartHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'

} finally {
    Assert-ChartBuildDirectory
    $chartConsumers = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('mono.exe', 'dotnet.exe', 'Unity.exe') -and $_.CommandLine -and
        ($_.CommandLine.Contains($chartBuild) -or $_.CommandLine.Contains($chartBuild.Replace('\', '/')))
    })
    if ($chartConsumers.Count) { throw 'Sea-chart test binaries are still used by another process.' }
    $chartFreed = 0L
    $chartRemoved = [Collections.Generic.List[string]]::new()
    foreach ($chartName in @('SeaChartTests.exe', 'nunit.framework.dll')) {
        $chartFile = [IO.Path]::GetFullPath((Join-Path $chartBuild $chartName))
        if ([IO.Path]::GetDirectoryName($chartFile) -ne [IO.Path]::GetFullPath($chartBuild)) { throw 'Unexpected cleanup target.' }
        if (Test-Path -LiteralPath $chartFile) {
            $chartItem = Get-Item -LiteralPath $chartFile
            if ($chartItem.PSIsContainer -or ($chartItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw ('Refusing unexpected test artifact: ' + $chartFile)
            }
            $chartFreed += $chartItem.Length
            Remove-Item -LiteralPath $chartFile -Force
            if (Test-Path -LiteralPath $chartFile) { throw ('Sea-chart test cleanup failed: ' + $chartFile) }
            $chartRemoved.Add($chartName)
        }
    }
    if ((Test-Path -LiteralPath $chartBuild) -and @(Get-ChildItem -LiteralPath $chartBuild -Force).Count -eq 0) {
        Remove-Item -LiteralPath $chartBuild
    }
    if (Test-Path -LiteralPath $chartOutput) {
        [pscustomobject]@{ ReleasedBytes = $chartFreed; RemovedFiles = @($chartRemoved); EvidenceRetained = $true;
            MainProjectCleaned = $false; SharedNUnitRetained = $chartNunit } |
            ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $chartOutput 'cleanup.json') -Encoding utf8
    }
    Write-Output ('Cleaned task test binaries: ' + $chartFreed + ' bytes; logs and results retained.')
}