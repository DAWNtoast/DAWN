param(
    [string]$Output = 'D:/Game/outputs/card-list-by-school-20261023',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$DataOnly
)
$ErrorActionPreference = 'Stop'
$listWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$listOutput = [IO.Path]::GetFullPath($Output)
$listBuild = Join-Path $listOutput 'Build'
New-Item -ItemType Directory -Force -Path $listBuild | Out-Null
$listFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$listSources = @(Get-ChildItem -LiteralPath (Join-Path $listWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -Recurse | Sort-Object FullName)
$listSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'ExportCardList.cs')
$listSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot '../SpecialCards20261017/CatalogResourceCosts.cs')
foreach ($listName in @('CardSpecialCostPresentation.cs', 'CardSupplyCostPresentation.cs', 'CardOrnamentKind.cs')) {
    $listSources += Get-Item -LiteralPath (Join-Path $listWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/' + $listName))
}
$listHashes = @($listSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$listHashes | Export-Csv -LiteralPath (Join-Path $listOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$listExecutable = Join-Path $listBuild 'ExportCardList.exe'
$listArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9',
    '-noconfig', '-nostdlib', "-out:$listExecutable")
foreach ($listAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Web.Extensions.dll')) {
    $listArguments += '-r:' + (Join-Path $listFramework $listAssembly)
}
$listArguments += $listSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @listArguments 2>&1 |
    Tee-Object -FilePath (Join-Path $listOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Card list exporter compilation failed.' }
foreach ($listHash in $listHashes) {
    if ((Get-FileHash -LiteralPath $listHash.Source -Algorithm SHA256).Hash -ne $listHash.SHA256) {
        throw "Source changed during compilation: $($listHash.Source). Export again after edits finish."
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $listExecutable $listOutput 2>&1 |
    Tee-Object -FilePath (Join-Path $listOutput 'export.log')
if ($LASTEXITCODE -ne 0) { throw 'Card list export verification failed.' }
if ($DataOnly) { return }
& (Join-Path $PSScriptRoot 'RenderReport.ps1') -DataPath (Join-Path $listOutput 'card-list-data.json') -OutputPath (Join-Path $listOutput '全部卡牌清单.html')
Get-Item -LiteralPath (Join-Path $listOutput '全部卡牌清单.html'), (Join-Path $listOutput '全部卡牌清单.md') |
    Select-Object FullName, Length
