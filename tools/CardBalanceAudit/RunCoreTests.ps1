param(
    [string]$Output = 'D:/Game/UnityClient/Artifacts/CardBalanceAudit20261012/CoreTests',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$ExportOnly
)
$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditOutput = [IO.Path]::GetFullPath($Output)
$auditBuild = Join-Path $auditOutput 'Build'
New-Item -ItemType Directory -Force -Path $auditBuild | Out-Null
$auditFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$auditCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$auditRuntime = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$auditMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$auditNunit = Get-ChildItem -LiteralPath (Join-Path $auditWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $auditNunit) { throw 'The Unity NUnit package cache is required. No editor process will be started.' }
Copy-Item -LiteralPath $auditNunit -Destination (Join-Path $auditBuild 'nunit.framework.dll')
$auditSources = @(Get-ChildItem -LiteralPath (Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$auditTestsRoot = Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
# Keep complete existing fixtures; never strip Unity methods or substitute assertion implementations.
$auditTests = @(Get-ChildItem -LiteralPath $auditTestsRoot -Filter '*.cs' | Where-Object {
    $_.Name -match 'Card|School|Battle|Naval|Boarding|CoreGuidedBossScript' -and $_.Name -notmatch 'Simulator' -and
    [IO.File]::ReadAllText($_.FullName) -notmatch 'UnityEngine|UnityEditor|ShatteredSail.Presentation'
} | Sort-Object Name)
$auditSources += Get-Item -LiteralPath (Join-Path $auditTestsRoot 'NavalBattleBalanceSimulator.cs')
$auditSources += $auditTests
$auditSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Runner.cs')
$auditHashes = @($auditSources | ForEach-Object { [pscustomobject]@{ Source=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$auditHashes |
    Export-Csv -LiteralPath (Join-Path $auditOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$auditTests.Name | Set-Content -LiteralPath (Join-Path $auditOutput 'fixtures.txt') -Encoding utf8
$auditExe = Join-Path $auditBuild 'CardBalanceCoreTests.exe'
$auditArguments = @($auditCompiler, '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$auditExe", "-r:$auditNunit")
foreach ($auditAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $auditArguments += '-r:' + (Join-Path $auditFramework $auditAssembly)
}
$auditArguments += $auditSources.FullName
& $auditRuntime @auditArguments 2>&1 | Tee-Object -FilePath (Join-Path $auditOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Card balance Core tests compilation failed.' }
foreach ($auditHash in $auditHashes) {
    if ((Get-FileHash -LiteralPath $auditHash.Source -Algorithm SHA256).Hash -ne $auditHash.SHA256) {
        throw "Source changed during compilation: $($auditHash.Source). Run again after the edit finishes."
    }
}
$auditRunArguments = @($auditExe, $auditOutput)
if ($ExportOnly) { $auditRunArguments += '--export-only' }
& $auditMono @auditRunArguments 2>&1 | Tee-Object -FilePath (Join-Path $auditOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Card balance Core tests failed. See $auditOutput/results.xml and tests.log." }
