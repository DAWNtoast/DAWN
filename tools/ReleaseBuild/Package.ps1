param(
    [Parameter(Mandatory = $true)][string]$BuildDirectory,
    [Parameter(Mandatory = $true)][string]$ZipPath,
    [Parameter(Mandatory = $false)][string]$ExecutableName = 'ShatteredSail.exe'
)

$ErrorActionPreference = 'Stop'
$releaseRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
$releaseZip = [System.IO.Path]::GetFullPath($ZipPath)
$executableLeaf = [System.IO.Path]::GetFileName($ExecutableName)
$dataDirectoryLeaf = [System.IO.Path]::GetFileNameWithoutExtension($executableLeaf) + '_Data'
if ([string]::IsNullOrWhiteSpace($ExecutableName) -or
    -not [string]::Equals($executableLeaf, $ExecutableName, [StringComparison]::Ordinal) -or
    -not [string]::Equals([System.IO.Path]::GetExtension($ExecutableName), '.exe', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ExecutableName must be a single .exe file name, not a path.'
}
if ($releaseZip.StartsWith($releaseRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The ZIP must be placed outside the build directory to avoid including itself.'
}
if (Test-Path -LiteralPath $releaseZip) { throw "Refusing to overwrite existing package: $releaseZip" }
foreach ($required in @($executableLeaf, 'UnityPlayer.dll', $dataDirectoryLeaf, 'MonoBleedingEdge', 'build-report.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot $required))) { throw "Missing runtime dependency: $required" }
}
if ((Get-Content -LiteralPath (Join-Path $releaseRoot 'build-report.txt') -Raw) -notmatch 'Result: Succeeded') {
    throw 'Unity build report is not successful.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($releaseRoot, $releaseZip, [System.IO.Compression.CompressionLevel]::Optimal, $true)

# Read every archive entry back and compare content, not just zip/exe existence.
$releaseArchive = [System.IO.Compression.ZipFile]::OpenRead($releaseZip)
$releaseVerified = 0
$releaseBytes = 0L
try {
    $releaseBase = Split-Path -Path $releaseRoot -Leaf
    $releaseExpected = @(Get-ChildItem -LiteralPath $releaseRoot -File -Recurse)
    foreach ($releaseEntry in $releaseArchive.Entries) {
        if ([string]::IsNullOrEmpty($releaseEntry.Name)) { continue }
        $releaseEntryPath = $releaseEntry.FullName.Replace('\', '/')
        if (-not $releaseEntryPath.StartsWith($releaseBase + '/', [StringComparison]::Ordinal)) { throw 'Unexpected archive prefix.' }
        $releaseRelative = $releaseEntryPath.Substring($releaseBase.Length + 1)
        $releaseDiskPath = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $releaseRelative))
        if (-not $releaseDiskPath.StartsWith($releaseRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Archive contains a path outside the selected build.'
        }
        $releaseDiskHash = (Get-FileHash -LiteralPath $releaseDiskPath -Algorithm SHA256).Hash
        $releaseHasher = [System.Security.Cryptography.SHA256]::Create()
        $releaseStream = $releaseEntry.Open()
        try {
            $releaseEntryHash = [BitConverter]::ToString($releaseHasher.ComputeHash($releaseStream)).Replace('-', '')
        } finally {
            $releaseStream.Dispose()
            $releaseHasher.Dispose()
        }
        if ($releaseDiskHash -ne $releaseEntryHash) { throw "Archive verification failed: $releaseRelative" }
        $releaseVerified++
        $releaseBytes += $releaseEntry.Length
    }
    if ($releaseVerified -ne $releaseExpected.Count) { throw 'Archive file count does not match the complete build.' }
} finally {
    $releaseArchive.Dispose()
}

[pscustomobject]@{
    Result = 'PASS'
    Zip = $releaseZip
    FilesVerified = $releaseVerified
    UncompressedBytes = $releaseBytes
    ZipBytes = (Get-Item -LiteralPath $releaseZip).Length
    ZipSHA256 = (Get-FileHash -LiteralPath $releaseZip -Algorithm SHA256).Hash
    ExeSHA256 = (Get-FileHash -LiteralPath (Join-Path $releaseRoot $executableLeaf) -Algorithm SHA256).Hash
} | ConvertTo-Json
