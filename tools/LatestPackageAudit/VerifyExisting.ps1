param(
    [string]$BuildDirectory = 'D:\Game\Builds\第七港口-火药炮弹规则20261011\Windows',
    [string]$ZipPath = 'D:\Game\Builds\第七港口-火药炮弹规则20261011\第七港口-Windows.zip',
    [string]$OutputPath = 'D:\Game\Artifacts\LatestPackageReview20261011\original-package-verification.json'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$auditRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path.TrimEnd('\')
$auditBase = Split-Path -Path $auditRoot -Leaf
$auditFiles = @(Get-ChildItem -LiteralPath $auditRoot -File -Recurse)
$auditSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$auditArchive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
$auditBytes = 0L
try {
    foreach ($auditEntry in $auditArchive.Entries) {
        if (-not $auditEntry.Name) { continue }
        $auditName = $auditEntry.FullName.Replace('\','/')
        if (-not $auditName.StartsWith($auditBase + '/', [StringComparison]::Ordinal)) { throw 'Unexpected archive prefix.' }
        $auditRelative = $auditName.Substring($auditBase.Length + 1)
        if (-not $auditSeen.Add($auditRelative)) { throw "Duplicate archive path: $auditRelative" }
        $auditDiskPath = [IO.Path]::GetFullPath((Join-Path $auditRoot $auditRelative))
        if (-not $auditDiskPath.StartsWith($auditRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes build.' }
        $auditDiskHash = (Get-FileHash -LiteralPath $auditDiskPath -Algorithm SHA256).Hash
        $auditHasher = [Security.Cryptography.SHA256]::Create()
        $auditStream = $auditEntry.Open()
        try { $auditEntryHash = [BitConverter]::ToString($auditHasher.ComputeHash($auditStream)).Replace('-','') }
        finally { $auditStream.Dispose(); $auditHasher.Dispose() }
        if ($auditDiskHash -ne $auditEntryHash) { throw "Hash mismatch: $auditRelative" }
        $auditBytes += $auditEntry.Length
    }
    if ($auditSeen.Count -ne $auditFiles.Count) { throw 'Archive and disk file counts differ.' }
    foreach ($auditFile in $auditFiles) {
        if (-not $auditSeen.Contains($auditFile.FullName.Substring($auditRoot.Length + 1).Replace('\','/'))) { throw 'File missing from archive.' }
    }
} finally { $auditArchive.Dispose() }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
$auditResult = [pscustomobject]@{Result='PASS';FilesVerified=$auditSeen.Count;UncompressedBytes=$auditBytes;ZipBytes=(Get-Item -LiteralPath $ZipPath).Length;ZipSHA256=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash;Build=$auditRoot;Zip=$ZipPath}
$auditResult | ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding utf8
$auditResult | ConvertTo-Json
