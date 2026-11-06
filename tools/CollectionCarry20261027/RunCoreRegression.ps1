param([string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$carryWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$carryOutput = [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'outputs/collection-carry-20261027/core'))
$carryBuild = Join-Path $carryOutput 'Build'
$carryFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$carryNunit = Get-ChildItem -LiteralPath (Join-Path $carryWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $carryNunit) { throw 'The existing Unity NUnit reference is missing.' }
$carryFixtures = @('CoreWarehouseCollectionTests', 'CoreWarehouseIncomeTests', 'CoreCollectionRewardCatalogTests', 'CoreVoyageSessionTests')
$carrySources = @(Get-ChildItem -LiteralPath (Join-Path $carryWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$carrySources += @($carryFixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $carryWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $_ + '.cs')) })
$carrySources += Get-Item -LiteralPath (Join-Path $carryWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$carrySources += Get-Item -LiteralPath (Join-Path $carryWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')

function Assert-CarryBuildPath {
    $carryExpected = [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'outputs/collection-carry-20261027/core/Build'))
    if ($carryBuild -ne $carryExpected) { throw 'Unexpected task build path.' }
    $carryCursor = $carryBuild
    while ($carryCursor) {
        if ((Test-Path -LiteralPath $carryCursor) -and
            ((Get-Item -LiteralPath $carryCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked path: ' + $carryCursor)
        }
        $carryCursor = Split-Path -Path $carryCursor -Parent
    }
}
Assert-CarryBuildPath
New-Item -ItemType Directory -Force -Path $carryBuild | Out-Null
Assert-CarryBuildPath
$carryHashes = @($carrySources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$carryHashes | Export-Csv -LiteralPath (Join-Path $carryOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$carryFixtures | Set-Content -LiteralPath (Join-Path $carryOutput 'fixtures.txt') -Encoding utf8
try {
    Copy-Item -LiteralPath $carryNunit -Destination (Join-Path $carryBuild 'nunit.framework.dll') -Force
    $carryExe = Join-Path $carryBuild 'CollectionCarryTests.exe'
    $carryArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$carryExe", "-r:$carryNunit")
    foreach ($carryAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
        $carryArgs += '-r:' + (Join-Path $carryFramework $carryAssembly)
    }
    $carryArgs += $carrySources.FullName
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @carryArgs 2>&1 | Tee-Object -FilePath (Join-Path $carryOutput 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Collection carry Core compilation failed.' }
    & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $carryExe $carryOutput 2>&1 | Tee-Object -FilePath (Join-Path $carryOutput 'tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Collection carry Core tests failed; see results.xml and failures.txt.' }
    foreach ($carryHash in $carryHashes) {
        if ((Get-FileHash -LiteralPath $carryHash.Source -Algorithm SHA256).Hash -ne $carryHash.SHA256) {
            throw ('Source changed during regression: ' + $carryHash.Source)
        }
    }
    'PASS current source hashes stable during Core regression.' | Tee-Object -FilePath (Join-Path $carryOutput 'validation.txt')
} finally {
    Assert-CarryBuildPath
    $carryConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe' OR Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/', '\').IndexOf($carryBuild.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($carryConsumers.Count -gt 0) { throw 'A process still references the task build; cleanup deferred.' }
    $carryBytes = 0L
    $carryRemoved = [Collections.Generic.List[string]]::new()
    foreach ($carryName in @('CollectionCarryTests.exe', 'nunit.framework.dll')) {
        $carryFile = [IO.Path]::GetFullPath((Join-Path $carryBuild $carryName))
        if ([IO.Path]::GetDirectoryName($carryFile) -ne $carryBuild) { throw 'Unexpected cleanup file.' }
        if (Test-Path -LiteralPath $carryFile) {
            $carryItem = Get-Item -LiteralPath $carryFile -Force
            if ($carryItem.PSIsContainer -or ($carryItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Refusing linked or non-file build artifact.' }
            $carryBytes += $carryItem.Length
            Remove-Item -LiteralPath $carryFile -Force
            if (Test-Path -LiteralPath $carryFile) { throw 'Cleanup did not remove the task artifact.' }
            $carryRemoved.Add($carryName)
        }
    }
    if (@(Get-ChildItem -LiteralPath $carryBuild -Force).Count -eq 0) { Remove-Item -LiteralPath $carryBuild -Force }
    [pscustomobject]@{ ReleasedBytes=$carryBytes; RemovedFiles=@($carryRemoved); BuildRemaining=(Test-Path -LiteralPath $carryBuild); MainProjectCleaned=$false; EvidenceRetained=$true } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $carryOutput 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task build artifacts: ' + $carryBytes + ' bytes; regression evidence retained.')
}
