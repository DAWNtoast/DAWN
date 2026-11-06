$ErrorActionPreference = 'Stop'
$helmRoot = 'D:\Game\tmp\window-fit-20261025'
$helmEvidence = 'D:\Game\Artifacts\NavigationHelm20261025\run1'
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($helmRoot) }).Count) { throw 'Validation project is occupied.' }
function Assert-RecoveryPath([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (!$absolute.StartsWith($helmRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside validation root.' }
    for ($current = $absolute; $current -and $current.StartsWith($helmRoot, [StringComparison]::OrdinalIgnoreCase); $current = Split-Path -Parent $current) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked recovery path.' }
    }
}
$helmLeasePath = Join-Path $helmRoot 'BasicSupplyLayout20261025.lock'
Assert-RecoveryPath $helmLeasePath
$helmLease = [IO.File]::Open($helmLeasePath, 'Open', 'ReadWrite', 'None')
try {
    $helmLock = Join-Path $helmRoot 'Temp/UnityLockfile'
    Assert-RecoveryPath $helmLock
    $helmLockHandle = [IO.File]::Open($helmLock, 'Open', 'ReadWrite', 'None')
    $helmLockHandle.Dispose()
    Remove-Item -LiteralPath $helmLock -Force
    $helmBackupRoot = Join-Path $helmEvidence 'OriginalValidationFiles'
    $helmRestoreCount = 0
    foreach ($helmBackup in Get-ChildItem -LiteralPath $helmBackupRoot -Recurse -File) {
        $helmRelative = $helmBackup.FullName.Substring($helmBackupRoot.Length + 1)
        $helmTarget = Join-Path $helmRoot ('Assets/_Project/' + $helmRelative)
        Assert-RecoveryPath $helmTarget
        Copy-Item -LiteralPath $helmBackup.FullName -Destination $helmTarget -Force
        if ((Get-FileHash -LiteralPath $helmBackup.FullName).Hash -ne (Get-FileHash -LiteralPath $helmTarget).Hash) { throw 'Restore hash mismatch.' }
        $helmRestoreCount++
    }
    $helmFreed = 0L
    foreach ($helmName in @('NavigationHelmGraphic.cs','NavigationHelmGraphic.cs.meta','NavigationHelmSlider.cs','NavigationHelmSlider.cs.meta')) {
        $helmTarget = Join-Path $helmRoot ('Assets/_Project/Presentation/Runtime/' + $helmName)
        Assert-RecoveryPath $helmTarget
        if (Test-Path -LiteralPath $helmTarget) {
            $helmFreed += (Get-Item -LiteralPath $helmTarget).Length
            Remove-Item -LiteralPath $helmTarget -Force
        }
        if (Test-Path -LiteralPath $helmTarget) { throw 'Temporary source removal failed.' }
    }
    $helmTemp = Join-Path $helmRoot 'Temp'
    Assert-RecoveryPath $helmTemp
    $helmTempItems = @(Get-ChildItem -LiteralPath $helmTemp -Force -Recurse)
    if (@($helmTempItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Linked Temp content.' }
    $helmFreed += ($helmTempItems | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $helmTemp -Recurse -Force
    if (Test-Path -LiteralPath $helmTemp) { throw 'Temp still exists.' }
    @('Recovered after compilation failure left an unlocked UnityLockfile.',
        ('Restored original validation files: ' + $helmRestoreCount),
        'Removed four new NavigationHelm source/meta copies and the owned Temp cache.',
        ('Reclaimed bytes: ' + $helmFreed),
        'Main UnityClient was not written. Existing shared Library retained for window-fit and other tools.',
        'Original source backups and failed compilation log retained.') | Set-Content -LiteralPath (Join-Path $helmEvidence 'cleanup.txt') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $helmEvidence 'cleanup.txt')
} finally {
    $helmLease.Dispose()
    Remove-Item -LiteralPath $helmLeasePath -Force
}
