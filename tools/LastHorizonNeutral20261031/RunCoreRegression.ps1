param(
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$RunName = 'core'
)

$ErrorActionPreference = 'Stop'
if ($RunName -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*$') { throw 'RunName must be a simple task-local directory name.' }
$neutralWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$neutralTaskRoot = [IO.Path]::GetFullPath((Join-Path $neutralWorkspace 'outputs/last-horizon-neutral-20261031'))
$neutralOutput = [IO.Path]::GetFullPath((Join-Path $neutralTaskRoot ('validation/' + $RunName)))
$neutralBuild = Join-Path $neutralOutput 'Build'
$neutralFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$neutralDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$neutralMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$neutralCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$neutralNunit = Get-ChildItem -LiteralPath (Join-Path $neutralWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $neutralNunit) { throw 'The existing Unity NUnit reference is missing.' }
foreach ($neutralRequired in @($neutralDotnet, $neutralMono, $neutralCompiler)) {
    if (-not (Test-Path -LiteralPath $neutralRequired -PathType Leaf)) { throw ('Missing compiler/runtime: ' + $neutralRequired) }
}

$neutralFixtures = @('LegendaryWarshipTests', 'NeutralLegendarySparringTests',
    'CoreBattleRewardDescriptionTests', 'CoreCollectionRewardCatalogTests', 'CoreBoardingLootTests')
$neutralSources = @(Get-ChildItem -LiteralPath (Join-Path $neutralWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$neutralSources += @($neutralFixtures | ForEach-Object {
    Get-Item -LiteralPath (Join-Path $neutralWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $_ + '.cs'))
})
$neutralSources += Get-Item -LiteralPath (Join-Path $neutralWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$neutralSources += Get-Item -LiteralPath (Join-Path $neutralWorkspace 'UnityClient/Assets/_Project/Presentation/Runtime/CoreTreasureStoryCatalog.cs')
$neutralSources += Get-Item -LiteralPath (Join-Path $neutralWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')

function Assert-NeutralBuildPath {
    $neutralExpected = [IO.Path]::GetFullPath((Join-Path $neutralTaskRoot ('validation/' + $RunName + '/Build')))
    if ([IO.Path]::GetFullPath($neutralBuild) -ne $neutralExpected -or
        -not $neutralExpected.StartsWith($neutralTaskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected task build path.'
    }
    $neutralCursor = $neutralBuild
    while ($neutralCursor) {
        if ((Test-Path -LiteralPath $neutralCursor) -and
            ((Get-Item -LiteralPath $neutralCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked path: ' + $neutralCursor)
        }
        $neutralCursor = Split-Path -Path $neutralCursor -Parent
    }
}

Assert-NeutralBuildPath
if ((Test-Path -LiteralPath (Join-Path $neutralOutput 'compile.log')) -or
    (Test-Path -LiteralPath (Join-Path $neutralOutput 'results.xml')) -or
    (Test-Path -LiteralPath (Join-Path $neutralOutput 'source-hashes.csv'))) {
    throw 'Validation evidence already exists. Use a different RunName to preserve it.'
}
if ((Test-Path -LiteralPath $neutralBuild) -and @(Get-ChildItem -LiteralPath $neutralBuild -Force).Count -gt 0) {
    throw 'Task build directory is not empty; inspect existing files before reuse.'
}
New-Item -ItemType Directory -Force -Path $neutralBuild | Out-Null
Assert-NeutralBuildPath
$neutralExe = Join-Path $neutralBuild 'NeutralLegendarySparringTests.exe'
$neutralResponse = Join-Path $neutralBuild 'compile.rsp'
$neutralBuildFiles = @('NeutralLegendarySparringTests.exe', 'nunit.framework.dll', 'compile.rsp')

try {
    $neutralHashes = @($neutralSources | ForEach-Object {
        [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $neutralHashes | Export-Csv -LiteralPath (Join-Path $neutralOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    $neutralFixtures | Set-Content -LiteralPath (Join-Path $neutralOutput 'fixtures.txt') -Encoding utf8
    Copy-Item -LiteralPath $neutralNunit -Destination (Join-Path $neutralBuild 'nunit.framework.dll')
    $neutralArgs = @('-nologo', '-target:exe', '-langversion:9', '-nostdlib',
        ('-out:"' + $neutralExe + '"'), ('-r:"' + $neutralNunit + '"'))
    foreach ($neutralAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
        $neutralArgs += '-r:"' + (Join-Path $neutralFramework $neutralAssembly) + '"'
    }
    $neutralArgs += @($neutralSources | ForEach-Object { '"' + $_.FullName + '"' })
    [IO.File]::WriteAllLines($neutralResponse, $neutralArgs, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $neutralOutput 'compile.log'), [string]::Empty, [Text.UTF8Encoding]::new($false))
    & $neutralDotnet $neutralCompiler '-noconfig' ('@' + $neutralResponse) 2>&1 | Tee-Object -FilePath (Join-Path $neutralOutput 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Neutral legendary Core compilation failed.' }
    & $neutralMono $neutralExe $neutralOutput 2>&1 | Tee-Object -FilePath (Join-Path $neutralOutput 'tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Neutral legendary regression failed; see results.xml and failures.txt.' }
    foreach ($neutralHash in $neutralHashes) {
        if ((Get-FileHash -LiteralPath $neutralHash.Source -Algorithm SHA256).Hash -ne $neutralHash.SHA256) {
            throw ('Source changed during regression: ' + $neutralHash.Source)
        }
    }
    'PASS current source hashes stable during neutral legendary regression.' |
        Tee-Object -FilePath (Join-Path $neutralOutput 'validation.txt')
} finally {
    Assert-NeutralBuildPath
    $neutralConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe' OR Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/', '\').IndexOf($neutralBuild.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($neutralConsumers.Count -gt 0) { throw 'A process still references the task build; cleanup deferred.' }
    $neutralBytes = 0L
    $neutralRemoved = [Collections.Generic.List[string]]::new()
    foreach ($neutralName in $neutralBuildFiles) {
        $neutralFile = [IO.Path]::GetFullPath((Join-Path $neutralBuild $neutralName))
        if ([IO.Path]::GetDirectoryName($neutralFile) -ne $neutralBuild) { throw 'Unexpected cleanup file.' }
        if (Test-Path -LiteralPath $neutralFile) {
            $neutralItem = Get-Item -LiteralPath $neutralFile -Force
            if ($neutralItem.PSIsContainer -or ($neutralItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Refusing linked or non-file build artifact.'
            }
            $neutralBytes += $neutralItem.Length
            Remove-Item -LiteralPath $neutralFile -Force
            if (Test-Path -LiteralPath $neutralFile) { throw 'Cleanup did not remove the task artifact.' }
            $neutralRemoved.Add($neutralName)
        }
    }
    if (@(Get-ChildItem -LiteralPath $neutralBuild -Force).Count -eq 0) { Remove-Item -LiteralPath $neutralBuild -Force }
    [pscustomobject]@{ ReleasedBytes = $neutralBytes; RemovedFiles = @($neutralRemoved); BuildRemaining = (Test-Path -LiteralPath $neutralBuild);
        MainProjectCleaned = $false; EvidenceRetained = $true } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $neutralOutput 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task build artifacts: ' + $neutralBytes + ' bytes; regression evidence retained.')
}
