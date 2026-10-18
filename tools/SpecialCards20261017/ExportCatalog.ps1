param(
    [string]$Output = 'D:/Game/Artifacts/SpecialCardRedesign20261017/Catalog',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$catalogWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$catalogOutput = [IO.Path]::GetFullPath($Output)
$catalogBuild = Join-Path $catalogOutput 'Build'
New-Item -ItemType Directory -Force -Path $catalogBuild | Out-Null
$catalogFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$catalogSources = @(Get-ChildItem -LiteralPath (Join-Path $catalogWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$catalogSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'ExportCatalog.cs')
$catalogHashes = @($catalogSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$catalogHashes | Export-Csv -LiteralPath (Join-Path $catalogOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$catalogExecutable = Join-Path $catalogBuild 'ExportSpecialCardCatalog.exe'
$catalogArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9',
    '-noconfig', '-nostdlib', "-out:$catalogExecutable")
foreach ($catalogAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $catalogArguments += '-r:' + (Join-Path $catalogFramework $catalogAssembly)
}
$catalogArguments += $catalogSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @catalogArguments 2>&1 |
    Tee-Object -FilePath (Join-Path $catalogOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Card catalog exporter compilation failed.' }
foreach ($catalogHash in $catalogHashes) {
    if ((Get-FileHash -LiteralPath $catalogHash.Source -Algorithm SHA256).Hash -ne $catalogHash.SHA256) {
        throw "Source changed during compilation: $($catalogHash.Source). Run again once edits finish."
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $catalogExecutable $catalogWorkspace 2>&1 |
    Tee-Object -FilePath (Join-Path $catalogOutput 'export.log')
if ($LASTEXITCODE -ne 0) { throw 'Card catalog export or source-card invariant verification failed.' }
Get-Item -LiteralPath (Join-Path $catalogWorkspace 'docs/CardCatalog20261017.md'),
    (Join-Path $catalogWorkspace 'docs/SpecialCardRedesign20261017.md') |
    ForEach-Object { [pscustomobject]@{ Document = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } } |
    Export-Csv -LiteralPath (Join-Path $catalogOutput 'document-hashes.csv') -Encoding utf8 -NoTypeInformation
