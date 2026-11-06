param(
    [string]$Output = 'D:/Game/outputs/card-list-by-school-20261023',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$DataOnly
)
$ErrorActionPreference = 'Stop'
$listWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$listOutput = [IO.Path]::GetFullPath($Output)
$listBuild = Join-Path $listOutput ('Build-' + [Guid]::NewGuid().ToString('N'))
$listBuildCreated = $false
try {
    New-Item -ItemType Directory -Force -Path $listOutput | Out-Null
    New-Item -ItemType Directory -Path $listBuild | Out-Null
    $listBuildCreated = $true
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
    $listMarkdown = [IO.File]::ReadAllText((Join-Path $listOutput '全部卡牌清单.md'))
    $listPlainText = ($listMarkdown -creplace '(?m)^#{1,3} ', '' -creplace '(?m)^- ', '  ').Replace('**', '')
    [IO.File]::WriteAllText((Join-Path $listOutput '全部卡牌清单.txt'), $listPlainText, [Text.UTF8Encoding]::new($false))
    if ($DataOnly) { return }
    & (Join-Path $PSScriptRoot '../SchoolUpgradeFocus20261024/ExportUpgradeList.ps1') -Output $listOutput -BaseCatalog (Join-Path $listOutput 'card-list-data.json') -UnityData $UnityData
    & (Join-Path $PSScriptRoot 'ExportCardArt.ps1') -Output $listOutput -UnityData $UnityData
    # Keep the visible game card faces synchronized with the same final definitions.
    # The exporter reuses verified renders when production sources and assets match.
    & (Join-Path $PSScriptRoot '../CardGameView20261030/ExportGameViews.ps1') -Output $listOutput -Unity (Join-Path (Split-Path -Parent $UnityData) 'Unity.exe')
    & (Join-Path $PSScriptRoot 'RenderReport.ps1') -DataPath (Join-Path $listOutput 'card-list-data.json') -OutputPath (Join-Path $listOutput '全部卡牌清单.html')
    Get-Item -LiteralPath (Join-Path $listOutput '全部卡牌清单.html'), (Join-Path $listOutput '全部卡牌清单.md') |
        Select-Object FullName, Length
} finally {
    # Delete only this invocation's known compiler output, never recurse through
    # user content, another run's cache, directory links, or the main project.
    if ($listBuildCreated -and (Test-Path -LiteralPath $listBuild)) {
        try {
            $listCleanupPath = [IO.Path]::GetFullPath($listBuild).TrimEnd('\', '/')
            $listCleanupParent = [IO.Path]::GetFullPath($listOutput).TrimEnd('\', '/')
            $listMainProject = [IO.Path]::GetFullPath((Join-Path $listWorkspace 'UnityClient')).TrimEnd('\', '/')
            if ([IO.Path]::GetDirectoryName($listCleanupPath) -ne $listCleanupParent -or
                $listCleanupPath -eq $listMainProject -or
                $listCleanupPath.StartsWith($listMainProject + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Build cleanup target is outside its output directory or inside UnityClient.'
            }
            $listCleanupAncestor = Get-Item -LiteralPath $listCleanupPath -Force
            while ($null -ne $listCleanupAncestor) {
                if (($listCleanupAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Build cleanup refuses directory links: ' + $listCleanupAncestor.FullName)
                }
                $listCleanupAncestor = $listCleanupAncestor.Parent
            }
            $listCleanupItems = @(Get-ChildItem -LiteralPath $listCleanupPath -Force)
            foreach ($listCleanupItem in $listCleanupItems) {
                if ($listCleanupItem.PSIsContainer -or
                    ($listCleanupItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                    $listCleanupItem.Name -ne 'ExportCardList.exe') {
                    throw ('Unexpected build content retained: ' + $listCleanupItem.FullName)
                }
            }
            $listCleanupProcesses = @(Get-CimInstance Win32_Process | Where-Object {
                $_.CommandLine -and $_.CommandLine.IndexOf($listCleanupPath, [StringComparison]::OrdinalIgnoreCase) -ge 0
            })
            if ($listCleanupProcesses.Count -gt 0) { throw 'Build directory is still referenced by a running process.' }
            $listCleanupBytes = 0L
            foreach ($listCleanupItem in $listCleanupItems) {
                # An exclusive probe also detects an executable/file held by a tool.
                $listCleanupStream = [IO.File]::Open($listCleanupItem.FullName, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                $listCleanupStream.Dispose()
                $listCleanupBytes += $listCleanupItem.Length
                Remove-Item -LiteralPath $listCleanupItem.FullName -Force
            }
            # Empty-only deletion refuses any unexpected new content without prompting.
            [IO.Directory]::Delete($listCleanupPath, $false)
            if (Test-Path -LiteralPath $listCleanupPath) { throw 'Build cleanup verification failed.' }
            ('Removed temporary build: {0}; released {1} bytes; logs and source hashes retained.' -f $listCleanupPath, $listCleanupBytes) |
                Set-Content -LiteralPath (Join-Path $listOutput 'build-cleanup.log') -Encoding utf8
        } catch {
            $listCleanupWarning = 'Temporary build retained for inspection: ' + $listBuild + '. ' + $_.Exception.Message
            Write-Warning $listCleanupWarning
            $listCleanupWarning | Set-Content -LiteralPath (Join-Path $listOutput 'build-cleanup.log') -Encoding utf8
        }
    }
}
