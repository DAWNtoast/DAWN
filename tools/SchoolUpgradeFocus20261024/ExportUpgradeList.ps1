param(
    [string]$Output = 'D:/Game/outputs/school-upgrade-focus-20261024/dev',
    [string]$BaseCatalog = '',
    [string]$CoreSource = '',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$upgradeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$upgradeOutput = [IO.Path]::GetFullPath($Output)
if (-not $CoreSource) { $CoreSource = Join-Path $upgradeWorkspace 'UnityClient/Assets/_Project/Core/Runtime' }
if (-not $BaseCatalog) { $BaseCatalog = Join-Path $upgradeOutput 'card-list-data.json' }
$upgradeCore = [IO.Path]::GetFullPath($CoreSource)
$upgradeBase = [IO.Path]::GetFullPath($BaseCatalog)
if (-not (Test-Path -LiteralPath $upgradeBase -PathType Leaf)) { throw "Export the matching base catalogue first: $upgradeBase" }
$upgradeBuild = Join-Path $upgradeOutput ('UpgradeBuild-' + [Guid]::NewGuid().ToString('N'))
$upgradeBuildCreated = $false
try {
    New-Item -ItemType Directory -Force -Path $upgradeOutput | Out-Null
    New-Item -ItemType Directory -Path $upgradeBuild | Out-Null
    $upgradeBuildCreated = $true
    $upgradeFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
    $upgradeSources = @(Get-ChildItem -LiteralPath $upgradeCore -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($upgradeSources.Count -eq 0) { throw "No Core source files: $upgradeCore" }
    $upgradeSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'ExportUpgradeList.cs')
    $upgradeSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot '../CardListBySchool20261023/ExportCardList.cs')
    $upgradeSources += Get-Item -LiteralPath (Join-Path $PSScriptRoot '../SpecialCards20261017/CatalogResourceCosts.cs')
    foreach ($upgradeName in @('CardSpecialCostPresentation.cs', 'CardSupplyCostPresentation.cs', 'CardOrnamentKind.cs')) {
        $upgradeSources += Get-Item -LiteralPath (Join-Path $upgradeWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/' + $upgradeName))
    }
    $upgradeHashes = @($upgradeSources | ForEach-Object {
        [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $upgradeHashes | Export-Csv -LiteralPath (Join-Path $upgradeOutput 'upgrade-source-hashes.csv') -Encoding utf8 -NoTypeInformation
    $upgradeBaseHash = (Get-FileHash -LiteralPath $upgradeBase -Algorithm SHA256).Hash
    $upgradeExecutable = Join-Path $upgradeBuild 'ExportUpgradeList.exe'
    $upgradeArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-main:ExportUpgradeList', '-langversion:9',
        '-noconfig', '-nostdlib', "-out:$upgradeExecutable")
    foreach ($upgradeAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Web.Extensions.dll')) {
        $upgradeArguments += '-r:' + (Join-Path $upgradeFramework $upgradeAssembly)
    }
    $upgradeArguments += $upgradeSources.FullName
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @upgradeArguments 2>&1 |
        Tee-Object -FilePath (Join-Path $upgradeOutput 'upgrade-compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Upgrade list exporter compilation failed.' }
    foreach ($upgradeHash in $upgradeHashes) {
        if ((Get-FileHash -LiteralPath $upgradeHash.Source -Algorithm SHA256).Hash -ne $upgradeHash.SHA256) {
            throw "Source changed during compilation: $($upgradeHash.Source). Export again after edits finish."
        }
    }
    & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $upgradeExecutable $upgradeOutput $upgradeBase 2>&1 |
        Tee-Object -FilePath (Join-Path $upgradeOutput 'upgrade-export.log')
    if ($LASTEXITCODE -ne 0) { throw 'Upgrade list export verification failed.' }
    if ((Get-FileHash -LiteralPath $upgradeBase -Algorithm SHA256).Hash -ne $upgradeBaseHash) { throw 'Base catalogue changed during export.' }
    foreach ($upgradeHash in $upgradeHashes) {
        if ((Get-FileHash -LiteralPath $upgradeHash.Source -Algorithm SHA256).Hash -ne $upgradeHash.SHA256) {
            throw "Source changed during export: $($upgradeHash.Source). Export again after edits finish."
        }
    }
    [pscustomobject]@{ BaseCatalog = $upgradeBase; SHA256 = $upgradeBaseHash; CoreSource = $upgradeCore } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $upgradeOutput 'upgrade-inputs.json') -Encoding utf8
    # Only append a navigation block when the matching base catalogue is in this output.
    # Keep the original card definitions, headings and JSON schema intact.
    $upgradeBaseMarkdown = Join-Path $upgradeOutput '全部卡牌清单.md'
    if ($upgradeBase -eq [IO.Path]::GetFullPath((Join-Path $upgradeOutput 'card-list-data.json')) -and
        (Test-Path -LiteralPath $upgradeBaseMarkdown -PathType Leaf)) {
        $upgradeText = [IO.File]::ReadAllText($upgradeBaseMarkdown)
        $upgradeText = [regex]::Replace($upgradeText, '(?s)\r?\n*<!-- card-upgrade-navigation -->.*?<!-- /card-upgrade-navigation -->\r?\n*', '')
        $upgradeLink = "`n`n<!-- card-upgrade-navigation -->`n强化成长另见 [全部卡牌强化清单](全部卡牌强化清单.html)（[Markdown](全部卡牌强化清单.md)）。`n<!-- /card-upgrade-navigation -->`n"
        [IO.File]::WriteAllText($upgradeBaseMarkdown, $upgradeText.TrimEnd() + $upgradeLink, [Text.UTF8Encoding]::new($false))
    }
    Get-Item -LiteralPath (Join-Path $upgradeOutput '全部卡牌强化清单.html'), (Join-Path $upgradeOutput '全部卡牌强化清单.md'),
        (Join-Path $upgradeOutput 'card-upgrade-data.json') | Select-Object FullName, Length
} finally {
    # Delete only this invocation's known compiler output, never recurse through
    # user content, another run's cache, directory links, or the main project.
    if ($upgradeBuildCreated -and (Test-Path -LiteralPath $upgradeBuild)) {
        try {
            $upgradeCleanupPath = [IO.Path]::GetFullPath($upgradeBuild).TrimEnd('\', '/')
            $upgradeCleanupParent = [IO.Path]::GetFullPath($upgradeOutput).TrimEnd('\', '/')
            $upgradeMainProject = [IO.Path]::GetFullPath((Join-Path $upgradeWorkspace 'UnityClient')).TrimEnd('\', '/')
            if ([IO.Path]::GetDirectoryName($upgradeCleanupPath) -ne $upgradeCleanupParent -or
                $upgradeCleanupPath -eq $upgradeMainProject -or
                $upgradeCleanupPath.StartsWith($upgradeMainProject + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Build cleanup target is outside its output directory or inside UnityClient.'
            }
            $upgradeCleanupAncestor = Get-Item -LiteralPath $upgradeCleanupPath -Force
            while ($null -ne $upgradeCleanupAncestor) {
                if (($upgradeCleanupAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Build cleanup refuses directory links: ' + $upgradeCleanupAncestor.FullName)
                }
                $upgradeCleanupAncestor = $upgradeCleanupAncestor.Parent
            }
            $upgradeCleanupItems = @(Get-ChildItem -LiteralPath $upgradeCleanupPath -Force)
            foreach ($upgradeCleanupItem in $upgradeCleanupItems) {
                if ($upgradeCleanupItem.PSIsContainer -or
                    ($upgradeCleanupItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                    $upgradeCleanupItem.Name -ne 'ExportUpgradeList.exe') {
                    throw ('Unexpected build content retained: ' + $upgradeCleanupItem.FullName)
                }
            }
            $upgradeCleanupProcesses = @(Get-CimInstance Win32_Process | Where-Object {
                $_.CommandLine -and $_.CommandLine.IndexOf($upgradeCleanupPath, [StringComparison]::OrdinalIgnoreCase) -ge 0
            })
            if ($upgradeCleanupProcesses.Count -gt 0) { throw 'Build directory is still referenced by a running process.' }
            $upgradeCleanupBytes = 0L
            foreach ($upgradeCleanupItem in $upgradeCleanupItems) {
                # An exclusive probe also detects an executable/file held by a tool.
                $upgradeCleanupStream = [IO.File]::Open($upgradeCleanupItem.FullName, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
                $upgradeCleanupStream.Dispose()
                $upgradeCleanupBytes += $upgradeCleanupItem.Length
                Remove-Item -LiteralPath $upgradeCleanupItem.FullName -Force
            }
            # Empty-only deletion refuses any unexpected new content without prompting.
            [IO.Directory]::Delete($upgradeCleanupPath, $false)
            if (Test-Path -LiteralPath $upgradeCleanupPath) { throw 'Build cleanup verification failed.' }
            ('Removed temporary build: {0}; released {1} bytes; logs and source hashes retained.' -f $upgradeCleanupPath, $upgradeCleanupBytes) |
                Set-Content -LiteralPath (Join-Path $upgradeOutput 'upgrade-build-cleanup.log') -Encoding utf8
        } catch {
            $upgradeCleanupWarning = 'Temporary build retained for inspection: ' + $upgradeBuild + '. ' + $_.Exception.Message
            Write-Warning $upgradeCleanupWarning
            $upgradeCleanupWarning | Set-Content -LiteralPath (Join-Path $upgradeOutput 'upgrade-build-cleanup.log') -Encoding utf8
        }
    }
}
