param(
    [string]$Output = 'D:/Game/outputs/crew-review-20261030',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$Python = 'C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
)
$ErrorActionPreference = 'Stop'
$crewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$crewOutput = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $crewOutput | Out-Null
$crewBuild = Join-Path $crewOutput ('DataBuild-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $crewBuild | Out-Null
try {
    $crewSources = @(Get-ChildItem -LiteralPath (Join-Path $crewWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -Recurse | Sort-Object FullName)
    $crewSources += Get-Item -LiteralPath (Join-Path $crewWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
    $crewSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'export_data.cs')
    $crewSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot '../SpecialCards20261017/CatalogResourceCosts.cs')
    foreach ($crewName in @('CardSpecialCostPresentation.cs', 'CardSupplyCostPresentation.cs', 'CardOrnamentKind.cs')) {
        $crewSources += Get-Item -LiteralPath (Join-Path $crewWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/' + $crewName))
    }
    $crewHashes = @($crewSources | ForEach-Object { [pscustomobject]@{ source = $_.FullName; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
    $crewHashes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $crewOutput 'data-source-hashes.json') -Encoding utf8
    $crewExecutable = Join-Path $crewBuild 'export_data.exe'
    $crewFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
    $crewArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$crewExecutable")
    foreach ($crewAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Web.Extensions.dll')) { $crewArguments += '-r:' + (Join-Path $crewFramework $crewAssembly) }
    $crewArguments += $crewSources.FullName
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @crewArguments 2>&1 | Tee-Object -FilePath (Join-Path $crewOutput 'data-compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Crew data exporter compilation failed.' }
    foreach ($crewHash in $crewHashes) { if ((Get-FileHash -LiteralPath $crewHash.source -Algorithm SHA256).Hash -ne $crewHash.sha256) { throw "Production source changed during compilation: $($crewHash.source)" } }
    & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $crewExecutable $crewOutput 2>&1 | Tee-Object -FilePath (Join-Path $crewOutput 'data-export.log')
    if ($LASTEXITCODE -ne 0) { throw 'Crew data export failed.' }
    & $Python (Join-Path $PSScriptRoot 'export_data_art.py') --root $crewWorkspace --output $crewOutput
    if ($LASTEXITCODE -ne 0) { throw 'Crew art export failed.' }
} finally {
    $crewResolved = [IO.Path]::GetFullPath($crewBuild).TrimEnd('\', '/')
    if ([IO.Path]::GetDirectoryName($crewResolved) -ne $crewOutput.TrimEnd('\', '/') -or (Split-Path -Leaf $crewResolved) -notlike 'DataBuild-*') { throw 'Unsafe data build cleanup target.' }
    $crewAncestor = Get-Item -LiteralPath $crewResolved -Force
    while ($null -ne $crewAncestor) {
        if (($crewAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing data build cleanup through directory links.' }
        $crewAncestor = $crewAncestor.Parent
    }
    $crewFiles = @(Get-ChildItem -LiteralPath $crewResolved -Force)
    foreach ($crewFile in $crewFiles) {
        if ($crewFile.PSIsContainer -or $crewFile.Name -ne 'export_data.exe' -or ($crewFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Unexpected build files retained.' }
    }
    $crewReferences = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($crewResolved, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($crewReferences.Count -gt 0) { throw 'Data build still referenced by a running process; retained.' }
    $crewReleased = 0L
    foreach ($crewFile in $crewFiles) {
        $crewStream = [IO.File]::Open($crewFile.FullName, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $crewStream.Dispose()
        $crewReleased += $crewFile.Length
        Remove-Item -LiteralPath $crewFile.FullName -Force
    }
    [IO.Directory]::Delete($crewResolved, $false)
    [pscustomobject]@{ removed = $crewResolved; releasedBytes = $crewReleased; mainProjectUntouched = $true; retained = '数据、素材、哈希、编译和导出日志' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $crewOutput 'data-cleanup.json') -Encoding utf8
}
