param([ValidateSet('Prepare', 'CopyBack')][string]$Action = 'Prepare')
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$taskSource = 'D:\Game\UnityClient'
$taskScratch = 'D:\Game\tmp\shipyard-detail-clean-20261011'
$taskEvidence = 'D:\Game\Artifacts\ShipArtRedraw20261012\validation\saved-preview-sync'
$taskBackup = 'D:\Game\Artifacts\ShipArtRedraw20261012\preview-scene-backup'
$taskReportPath = Join-Path $taskEvidence 'copyback-report.json'
$taskSync = Get-Content -LiteralPath (Join-Path $taskEvidence 'report.json') -Raw | ConvertFrom-Json
$taskUtf8 = [System.Text.UTF8Encoding]::new($false)
if ($Action -eq 'Prepare') {
    if (Test-Path -LiteralPath $taskReportPath) { throw 'Copyback report already exists; preserve its original hashes.' }
    foreach ($taskScene in $taskSync.scenes | Where-Object { $_.saved -and $_.kind -eq 'Battle' }) {
        $taskOriginal = [System.IO.File]::ReadAllText((Join-Path $taskSource $taskScene.path))
        $taskNewPath = Join-Path $taskScratch $taskScene.path
        $taskNew = [System.IO.File]::ReadAllText($taskNewPath)
        foreach ($taskId in @(1750870022, 402127639, 725910849, 998859138, 1772893608, 1924198276)) {
            $taskPattern = '(?ms)^--- !u!114 &' + $taskId + '\r?\n.*?(?=^--- !u!|\z)'
            $taskOldBlock = [regex]::Matches($taskOriginal, $taskPattern)
            $taskNewBlock = [regex]::Matches($taskNew, $taskPattern)
            if ($taskOldBlock.Count -ne 1 -or $taskNewBlock.Count -ne 1) { throw "Expected one preserved outside-scope block $taskId in $($taskScene.path)." }
            $taskNew = $taskNew.Replace($taskNewBlock[0].Value, $taskOldBlock[0].Value)
        }
        [System.IO.File]::WriteAllText($taskNewPath, $taskNew, $taskUtf8)
    }
    # Unity reordered this already-correct material's float list; retain its
    # exact production bytes, since no dependency requires a semantic update.
    $taskMat = 'Assets/_Project/Presentation/EditablePreviewAssets/UI_Battle_Battle_Ship_Navy_Cutout__Instance__material.mat'
    Copy-Item -LiteralPath (Join-Path $taskSource $taskMat) -Destination (Join-Path $taskScratch $taskMat)
    $taskFiles = @()
    foreach ($taskScene in $taskSync.scenes | Where-Object saved) {
        $taskRelative = $taskScene.path
        $taskOldHash = (Get-FileHash -LiteralPath (Join-Path $taskSource $taskRelative)).Hash
        $taskNewHash = (Get-FileHash -LiteralPath (Join-Path $taskScratch $taskRelative)).Hash
        if ((Get-FileHash -LiteralPath (Join-Path $taskSource ($taskRelative + '.meta'))).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskScratch ($taskRelative + '.meta'))).Hash) { throw "Scene GUID changed: $taskRelative" }
        if ($taskOldHash -ne $taskNewHash) { $taskFiles += [pscustomobject]@{ path=$taskRelative; originalSha256=$taskOldHash; syncedSha256=$taskNewHash } }
    }
    foreach ($taskAsset in $taskSync.dependencies) {
        foreach ($taskRelative in @($taskAsset, ($taskAsset + '.meta'))) {
            if ((Get-FileHash -LiteralPath (Join-Path $taskSource $taskRelative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskScratch $taskRelative)).Hash) { throw "Unexpected editable dependency change: $taskRelative" }
        }
    }
    $taskReport = [pscustomobject]@{
        status='Prepared, awaiting read-only scope audit and saved-preview reopen capture'
        scratch=$taskScratch; backup=$taskBackup
        auditedScenes=33; syncedScenes=30; unchangedBoardingScenes=3; referencedEditableAssets=35; copiedEditableAssets=0
        cleanup='Five naval scenes restored six outside-scope MonoBehaviour blocks verbatim from production: Retreat Label Text and five Card Type Surface blocks. Reordered but semantically unchanged ship material restored byte-for-byte. Only ship/wake geometry, full-cell UVs and existing UI_Battle canvas-visible scale repair remain. All preview settings and scene/asset GUIDs unchanged.'
        boardingEvidence='All three boarding scenes have no RawImage using either changed atlas and zero serialized references to atlas GUIDs 397e249926814250bc6a4be698901b0b / 805abc374cfac654699a8582dbd3e690; production and scratch bytes equal.'
        files=$taskFiles
    }
    [System.IO.File]::WriteAllText($taskReportPath, ($taskReport | ConvertTo-Json -Depth 6), $taskUtf8)
    [System.IO.File]::WriteAllLines((Join-Path $taskEvidence 'changed-paths.txt'), [string[]]($taskFiles.path), $taskUtf8)
    Write-Output "Prepared $($taskFiles.Count) changed scene paths; 35 editable assets and all metas byte-identical."
    exit 0
}
$taskReport = Get-Content -LiteralPath $taskReportPath -Raw | ConvertFrom-Json
if ($taskReport.status -eq 'Copied and SHA256 verified') { throw 'Copyback already completed.' }
foreach ($taskFile in $taskReport.files) {
    $taskRelative = $taskFile.path
    if (-not $taskRelative.StartsWith('Assets/_Project/Scenes/UIPreviewsForMe/')) { throw 'Copy path escaped authorized scene scope.' }
    $taskOldPath = [System.IO.Path]::GetFullPath((Join-Path $taskSource $taskRelative))
    $taskNewPath = [System.IO.Path]::GetFullPath((Join-Path $taskScratch $taskRelative))
    $taskBackupPath = [System.IO.Path]::GetFullPath((Join-Path $taskBackup $taskRelative))
    if (-not $taskBackupPath.StartsWith($taskBackup + '\')) { throw 'Backup path escaped artifact directory.' }
    if ((Get-FileHash -LiteralPath $taskOldPath).Hash -ne $taskFile.originalSha256 -or (Get-FileHash -LiteralPath $taskNewPath).Hash -ne $taskFile.syncedSha256) { throw "Scene changed after review: $taskRelative" }
    if (Test-Path -LiteralPath $taskBackupPath) { throw "Refusing to overwrite earlier backup: $taskRelative" }
}
foreach ($taskFile in $taskReport.files) {
    $taskOldPath = Join-Path $taskSource $taskFile.path
    $taskBackupPath = Join-Path $taskBackup $taskFile.path
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($taskBackupPath)) -Force | Out-Null
    Copy-Item -LiteralPath $taskOldPath -Destination $taskBackupPath
    Copy-Item -LiteralPath ($taskOldPath + '.meta') -Destination ($taskBackupPath + '.meta')
    Copy-Item -LiteralPath (Join-Path $taskScratch $taskFile.path) -Destination $taskOldPath
    if ((Get-FileHash -LiteralPath $taskOldPath).Hash -ne $taskFile.syncedSha256) { throw "Copy hash mismatch: $($taskFile.path)" }
}
$taskReport.status = 'Copied and SHA256 verified'
$taskReport | Add-Member -NotePropertyName copiedAtUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('O'))
[System.IO.File]::WriteAllText($taskReportPath, ($taskReport | ConvertTo-Json -Depth 6), $taskUtf8)
Write-Output "Copied $($taskReport.files.Count) scenes; backups: $taskBackup; no dependency/meta writes."
