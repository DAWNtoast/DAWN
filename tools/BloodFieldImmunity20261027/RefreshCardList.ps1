$ErrorActionPreference = 'Stop'
$revisionWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$revisionOutput = Join-Path $revisionWorkspace 'outputs/blood-field-immunity-20261027/card-list-export'
$revisionFixed = Join-Path $revisionWorkspace 'outputs/card-list-by-school-20261023'
$revisionBackup = Join-Path $revisionWorkspace 'outputs/blood-field-immunity-20261027/source-before/card-list'
New-Item -ItemType Directory -Path $revisionBackup -Force | Out-Null
$revisionArtifacts = @('全部卡牌清单.html','全部卡牌清单.md','card-list-data.json',
    '全部卡牌强化清单.html','全部卡牌强化清单.md','card-upgrade-data.json')
foreach ($revisionName in $revisionArtifacts) {
    $revisionOld = Join-Path $revisionFixed $revisionName
    $revisionSaved = Join-Path $revisionBackup $revisionName
    if ((Test-Path -LiteralPath $revisionOld) -and !(Test-Path -LiteralPath $revisionSaved)) {
        Copy-Item -LiteralPath $revisionOld -Destination $revisionSaved
    }
}
$revisionRemovedBytes = 0L
try {
    & (Join-Path $revisionWorkspace 'tools/CardListBySchool20261023/ExportCardList.ps1') -Output $revisionOutput
    foreach ($revisionName in $revisionArtifacts) {
        $revisionNew = Join-Path $revisionOutput $revisionName
        if (!(Test-Path -LiteralPath $revisionNew -PathType Leaf)) { throw ('Missing exported artifact: ' + $revisionName) }
    }
    foreach ($revisionName in $revisionArtifacts) {
        Copy-Item -LiteralPath (Join-Path $revisionOutput $revisionName) -Destination (Join-Path $revisionFixed $revisionName) -Force
    }
} finally {
    foreach ($revisionBuildName in @('Build', 'UpgradeBuild')) {
        $revisionBuild = [IO.Path]::GetFullPath((Join-Path $revisionOutput $revisionBuildName))
        if (!$revisionBuild.StartsWith([IO.Path]::GetFullPath($revisionOutput) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe export cleanup path.' }
        if (!(Test-Path -LiteralPath $revisionBuild)) { continue }
        foreach ($revisionAncestor in @($revisionWorkspace, (Join-Path $revisionWorkspace 'outputs'),
            (Split-Path $revisionOutput), $revisionOutput, $revisionBuild)) {
            if ((Get-Item -LiteralPath $revisionAncestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked export directory.' }
        }
        $revisionEntries = @(Get-ChildItem -LiteralPath $revisionBuild -Force -Recurse)
        if (@($revisionEntries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Linked export artifact.' }
        $revisionConsumers = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -in @('mono.exe','dotnet.exe','Unity.exe') -and $_.CommandLine -and
            ($_.CommandLine.Contains($revisionBuild) -or $_.CommandLine.Contains($revisionBuild.Replace('\','/')))
        })
        if ($revisionConsumers.Count) { throw 'Export build still in use.' }
        foreach ($revisionFile in $revisionEntries | Where-Object { !$_.PSIsContainer }) { $revisionRemovedBytes += $revisionFile.Length }
        Remove-Item -LiteralPath $revisionBuild -Recurse -Force
        if (Test-Path -LiteralPath $revisionBuild) { throw 'Export cache cleanup failed.' }
    }
    [pscustomobject]@{ RemovedBytes=$revisionRemovedBytes; MainProjectCleaned=$false; ReportsAndInputsPreserved=$true } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $revisionOutput 'cleanup.json') -Encoding utf8
}
