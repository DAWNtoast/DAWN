param(
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$RunName = 'core'
)

$ErrorActionPreference = 'Stop'
if ($RunName -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') { throw 'RunName must be a simple task-local directory name.' }
$fleetWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fleetTaskRoot = [IO.Path]::GetFullPath((Join-Path $fleetWorkspace 'outputs/legendary-comics-20261031'))
$fleetOutput = [IO.Path]::GetFullPath((Join-Path $fleetTaskRoot ('validation/' + $RunName)))
$fleetBuild = Join-Path $fleetOutput 'Build'
$fleetFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$fleetDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$fleetMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$fleetCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$fleetNunit = Get-ChildItem -LiteralPath (Join-Path $fleetWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $fleetNunit) { throw 'The existing Unity NUnit reference is missing.' }
foreach ($fleetRequired in @($fleetDotnet, $fleetMono, $fleetCompiler)) {
    if (-not (Test-Path -LiteralPath $fleetRequired -PathType Leaf)) { throw ('Missing compiler/runtime: ' + $fleetRequired) }
}

$fleetFixtures = @('LegendaryWarshipTests', 'NeutralLegendarySparringTests',
    'CoreBattleRewardDescriptionTests', 'CoreCollectionRewardCatalogTests', 'CoreBoardingLootTests',
    'LegendaryFleetComicCatalogTests')
$fleetSources = @(Get-ChildItem -LiteralPath (Join-Path $fleetWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$fleetSources += @($fleetFixtures | ForEach-Object {
    Get-Item -LiteralPath (Join-Path $fleetWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $_ + '.cs'))
})
$fleetSources += Get-Item -LiteralPath (Join-Path $fleetWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$fleetSources += Get-Item -LiteralPath (Join-Path $fleetWorkspace 'UnityClient/Assets/_Project/Presentation/Runtime/CoreTreasureStoryCatalog.cs')
$fleetSources += Get-Item -LiteralPath (Join-Path $fleetWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')

function Assert-FleetBuildPath {
    $fleetExpected = [IO.Path]::GetFullPath((Join-Path $fleetTaskRoot ('validation/' + $RunName + '/Build')))
    if ([IO.Path]::GetFullPath($fleetBuild) -ne $fleetExpected -or
        -not $fleetExpected.StartsWith($fleetTaskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected task build path.'
    }
    $fleetCursor = $fleetBuild
    while ($fleetCursor) {
        if ((Test-Path -LiteralPath $fleetCursor) -and
            ((Get-Item -LiteralPath $fleetCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked path: ' + $fleetCursor)
        }
        $fleetCursor = Split-Path -Path $fleetCursor -Parent
    }
}

Assert-FleetBuildPath
if ((Test-Path -LiteralPath (Join-Path $fleetOutput 'compile.log')) -or
    (Test-Path -LiteralPath (Join-Path $fleetOutput 'results.xml')) -or
    (Test-Path -LiteralPath (Join-Path $fleetOutput 'source-hashes.csv'))) {
    throw 'Validation evidence already exists. Use a different RunName to preserve it.'
}
if ((Test-Path -LiteralPath $fleetBuild) -and @(Get-ChildItem -LiteralPath $fleetBuild -Force).Count -gt 0) {
    throw 'Task build directory is not empty; inspect existing files before reuse.'
}
New-Item -ItemType Directory -Force -Path $fleetBuild | Out-Null
Assert-FleetBuildPath
$fleetExe = Join-Path $fleetBuild 'LegendaryFleetComicTests.exe'
$fleetResponse = Join-Path $fleetBuild 'compile.rsp'
$fleetBuildFiles = @('LegendaryFleetComicTests.exe', 'nunit.framework.dll', 'compile.rsp')

try {
    $fleetHashes = @($fleetSources | ForEach-Object {
        [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $fleetHashes | Export-Csv -LiteralPath (Join-Path $fleetOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    $fleetFixtures | Set-Content -LiteralPath (Join-Path $fleetOutput 'fixtures.txt') -Encoding utf8
    Copy-Item -LiteralPath $fleetNunit -Destination (Join-Path $fleetBuild 'nunit.framework.dll')
    $fleetArgs = @('-nologo', '-target:exe', '-langversion:9', '-nostdlib',
        ('-out:"' + $fleetExe + '"'), ('-r:"' + $fleetNunit + '"'))
    foreach ($fleetAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
        $fleetArgs += '-r:"' + (Join-Path $fleetFramework $fleetAssembly) + '"'
    }
    $fleetArgs += @($fleetSources | ForEach-Object { '"' + $_.FullName + '"' })
    [IO.File]::WriteAllLines($fleetResponse, $fleetArgs, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $fleetOutput 'compile.log'), [string]::Empty, [Text.UTF8Encoding]::new($false))
    & $fleetDotnet $fleetCompiler '-noconfig' ('@' + $fleetResponse) 2>&1 | Tee-Object -FilePath (Join-Path $fleetOutput 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Legendary fleet comic Core compilation failed.' }
    & $fleetMono $fleetExe $fleetOutput 2>&1 | Tee-Object -FilePath (Join-Path $fleetOutput 'tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Legendary fleet comic regression failed; see results.xml and failures.txt.' }
    foreach ($fleetHash in $fleetHashes) {
        if ((Get-FileHash -LiteralPath $fleetHash.Source -Algorithm SHA256).Hash -ne $fleetHash.SHA256) {
            throw ('Source changed during regression: ' + $fleetHash.Source)
        }
    }
    'PASS current source hashes stable during legendary fleet comic regression.' |
        Tee-Object -FilePath (Join-Path $fleetOutput 'validation.txt')
} finally {
    Assert-FleetBuildPath
    $fleetConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe' OR Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/', '\').IndexOf($fleetBuild.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($fleetConsumers.Count -gt 0) { throw 'A process still references the task build; cleanup deferred.' }
    $fleetBytes = 0L
    $fleetRemoved = [Collections.Generic.List[string]]::new()
    foreach ($fleetName in $fleetBuildFiles) {
        $fleetFile = [IO.Path]::GetFullPath((Join-Path $fleetBuild $fleetName))
        if ([IO.Path]::GetDirectoryName($fleetFile) -ne $fleetBuild) { throw 'Unexpected cleanup file.' }
        if (Test-Path -LiteralPath $fleetFile) {
            $fleetItem = Get-Item -LiteralPath $fleetFile -Force
            if ($fleetItem.PSIsContainer -or ($fleetItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Refusing linked or non-file build artifact.'
            }
            $fleetBytes += $fleetItem.Length
            Remove-Item -LiteralPath $fleetFile -Force
            if (Test-Path -LiteralPath $fleetFile) { throw 'Cleanup did not remove the task artifact.' }
            $fleetRemoved.Add($fleetName)
        }
    }
    if (@(Get-ChildItem -LiteralPath $fleetBuild -Force).Count -eq 0) { Remove-Item -LiteralPath $fleetBuild -Force }
    [pscustomobject]@{ ReleasedBytes = $fleetBytes; RemovedFiles = @($fleetRemoved); BuildRemaining = (Test-Path -LiteralPath $fleetBuild);
        MainProjectCleaned = $false; EvidenceRetained = $true } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $fleetOutput 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task build artifacts: ' + $fleetBytes + ' bytes; regression evidence retained.')
}
