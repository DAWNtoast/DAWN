param(
    [string]$Output = 'D:/Game/Artifacts/CardUpgradeDesign20261017/Catalog',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$upgradeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$upgradeOutput = [IO.Path]::GetFullPath($Output)
$upgradeBuild = Join-Path $upgradeOutput 'Build'
New-Item -ItemType Directory -Force -Path $upgradeBuild | Out-Null
$upgradeFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$upgradeSources = @(Get-ChildItem -LiteralPath (Join-Path $upgradeWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$upgradeSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'ExportUpgradeDesign.cs')
$upgradeHashes = @($upgradeSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$upgradeHashes | Export-Csv -LiteralPath (Join-Path $upgradeOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$upgradeExecutable = Join-Path $upgradeBuild 'ExportUpgradeDesign.exe'
$upgradeArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9',
    '-noconfig', '-nostdlib', "-out:$upgradeExecutable")
foreach ($upgradeAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $upgradeArguments += '-r:' + (Join-Path $upgradeFramework $upgradeAssembly)
}
$upgradeArguments += $upgradeSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @upgradeArguments 2>&1 |
    Tee-Object -FilePath (Join-Path $upgradeOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Card upgrade design exporter compilation failed.' }
foreach ($upgradeHash in $upgradeHashes) {
    if ((Get-FileHash -LiteralPath $upgradeHash.Source -Algorithm SHA256).Hash -ne $upgradeHash.SHA256) {
        throw "Source changed during compilation: $($upgradeHash.Source). Run again once edits finish."
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $upgradeExecutable $upgradeWorkspace $upgradeOutput 2>&1 |
    Tee-Object -FilePath (Join-Path $upgradeOutput 'export.log')
if ($LASTEXITCODE -ne 0) { throw 'Card upgrade design export failed; inspect validation.txt and failures.tsv.' }
Get-Item -LiteralPath (Join-Path $upgradeWorkspace 'docs/CardUpgradeDesign20261017.md') |
    ForEach-Object { [pscustomobject]@{ Document = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } } |
    Export-Csv -LiteralPath (Join-Path $upgradeOutput 'document-hashes.csv') -Encoding utf8 -NoTypeInformation
