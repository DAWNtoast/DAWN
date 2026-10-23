param(
    [string]$Output = 'D:/Game/Artifacts/EventDescriptionCatalog20261023',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$eventWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$eventOutput = [IO.Path]::GetFullPath($Output)
$eventBuild = Join-Path $eventOutput 'Build'
New-Item -ItemType Directory -Force -Path $eventBuild | Out-Null
$eventFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$eventSources = @(Get-ChildItem -LiteralPath (Join-Path $eventWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$eventSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Export.cs')
$eventSources += Get-Item -LiteralPath (Join-Path $eventWorkspace 'UnityClient/Assets/_Project/Presentation/Runtime/CoreTreasureStoryCatalog.cs')
$eventHashes = @($eventSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$eventHashes | Export-Csv -LiteralPath (Join-Path $eventOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
$eventExecutable = Join-Path $eventBuild 'ExportEventDescriptions.exe'
$eventArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9',
    '-noconfig', '-nostdlib', "-out:$eventExecutable")
foreach ($eventAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $eventArguments += '-r:' + (Join-Path $eventFramework $eventAssembly)
}
$eventArguments += $eventSources.FullName
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @eventArguments 2>&1 |
    Tee-Object -FilePath (Join-Path $eventOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Event description exporter compilation failed.' }
foreach ($eventHash in $eventHashes) {
    if ((Get-FileHash -LiteralPath $eventHash.Source -Algorithm SHA256).Hash -ne $eventHash.SHA256) {
        throw "Source changed during compilation: $($eventHash.Source)."
    }
}
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $eventExecutable $eventWorkspace 2>&1 |
    Tee-Object -FilePath (Join-Path $eventOutput 'export.log')
if ($LASTEXITCODE -ne 0) { throw 'Event description export failed.' }
$eventDocument = Join-Path $eventWorkspace 'docs/事件目录与文本描述_2026-10-23.md'
Get-FileHash -LiteralPath $eventDocument -Algorithm SHA256 |
    Export-Csv -LiteralPath (Join-Path $eventOutput 'document-hash.csv') -Encoding utf8 -NoTypeInformation
