param(
    [string]$Output = 'D:/Game/Artifacts/CardUpgradeLimits20261015',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$limitsWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$limitsOutput = [IO.Path]::GetFullPath($Output)
$limitsBuild = Join-Path $limitsOutput 'Build'
New-Item -ItemType Directory -Force -Path $limitsBuild | Out-Null
$limitsSources = @(Get-ChildItem -LiteralPath (Join-Path $limitsWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$limitsSources += Get-Item -LiteralPath (Join-Path $limitsWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$limitsSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'ExportCardUpgradeLimits.cs')
$limitsHashes = @($limitsSources | ForEach-Object { [pscustomobject]@{ Source=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
$limitsHashes | Export-Csv -LiteralPath (Join-Path $limitsOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$limitsFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$limitsExe = Join-Path $limitsBuild 'ExportCardUpgradeLimits.exe'
$limitsArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$limitsExe")
foreach ($limitsAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $limitsArgs += '-r:' + (Join-Path $limitsFramework $limitsAssembly)
}
$limitsArgs += $limitsSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @limitsArgs 2>&1 | Tee-Object -FilePath (Join-Path $limitsOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Card upgrade export compilation failed.' }
foreach ($limitsHash in $limitsHashes) {
    if ((Get-FileHash -LiteralPath $limitsHash.Source -Algorithm SHA256).Hash -ne $limitsHash.SHA256) {
        throw "Source changed during compilation: $($limitsHash.Source). Run again after the edit finishes."
    }
}
$limitsMarkdown = Join-Path $limitsWorkspace 'docs/CardUpgradeLimits20261015.md'
$limitsCsv = Join-Path $limitsWorkspace 'outputs/balance_configs/card_upgrade_limits_20261015.csv'
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $limitsExe $limitsMarkdown $limitsCsv 2>&1 | Tee-Object -FilePath (Join-Path $limitsOutput 'export.log')
if ($LASTEXITCODE -ne 0) { throw 'Card upgrade export or fixed-card audit failed. Inspect export.log.' }
