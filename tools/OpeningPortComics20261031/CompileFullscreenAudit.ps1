# Compile only the updated offline capture tool against the already validated
# isolated Unity assemblies. Does not rebuild production or touch Unity caches.
$ErrorActionPreference = 'Stop'
$workspace = 'D:\Game'
$scratch = Join-Path $workspace 'tmp\component-rarity-halo-20261019'
$output = Join-Path $workspace 'outputs\opening-port-comics-20261031\fullscreen-tool-compile'
$source = Join-Path $workspace 'tools\ComicFullscreenAudit\ComicFullscreenCapture.cs'
$unityData = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data'
$assemblies = Join-Path $scratch 'Library\ScriptAssemblies'
$refs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content -LiteralPath (Join-Path $workspace 'Artifacts\CardNumbering20261017\UiCompile\Presentation.rsp')) {
    if ($line -notmatch '^-r:"(.+)"$') { continue }
    $path = $Matches[1]
    $name = [IO.Path]::GetFileName($path).Replace('.ref.dll', '.dll')
    if ($path.StartsWith('Library/PackageCache/')) { $path = Join-Path $scratch $path }
    elseif ($path.StartsWith('Library/') -or $name.StartsWith('ShatteredSail.')) { $path = Join-Path $assemblies $name }
    if (!(Test-Path -LiteralPath $path)) { throw ('Missing read-only compilation dependency: ' + $path) }
    [void]$refs.Add([IO.Path]::GetFullPath($path))
}
[void]$refs.Add((Join-Path $assemblies 'ShatteredSail.Presentation.dll'))
foreach ($path in @($output, $source, $assemblies)) {
    for ($at = [IO.Path]::GetFullPath($path); $at; $at = Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Linked compilation path: ' + $at)
        }
    }
}
if (Test-Path -LiteralPath $output) { throw 'Retain the previous compile evidence; use this entry once after final Unity validation.' }
New-Item -ItemType Directory -Path $output | Out-Null
$dll = Join-Path $output 'ComicFullscreenCapture.dll'
$rsp = Join-Path $output 'compile.rsp'
$arguments = @('-target:library','-nostdlib+','-langversion:latest','-warn:4',('-out:"' + $dll + '"'))
$arguments += @($refs | Sort-Object | ForEach-Object { '-r:"' + $_ + '"' })
$arguments += '"' + $source + '"'
$arguments | Set-Content -LiteralPath $rsp -Encoding utf8
$arguments | Set-Content -LiteralPath (Join-Path $output 'arguments.txt') -Encoding utf8
Get-FileHash -LiteralPath (@($refs) + @($source)) | Select-Object Path,Hash |
    Export-Csv -LiteralPath (Join-Path $output 'input-hashes.csv') -NoTypeInformation -Encoding utf8
$passed = $false
$freed = 0L
try {
    & (Join-Path $unityData 'NetCoreRuntime\dotnet.exe') (Join-Path $unityData 'DotNetSdkRoslyn\csc.dll') -noconfig ('@' + $rsp) 2>&1 |
        Tee-Object -FilePath (Join-Path $output 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw ('Capture tool compile failed: ' + $LASTEXITCODE) }
    $passed = $true
} finally {
    foreach ($temporary in @($dll, $rsp)) {
        $resolved = [IO.Path]::GetFullPath($temporary)
        if (!$resolved.StartsWith($output + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup path.' }
        if (Test-Path -LiteralPath $resolved) {
            $freed += (Get-Item -LiteralPath $resolved).Length
            Remove-Item -LiteralPath $resolved -Force
        }
    }
    [pscustomobject]@{ Passed=$passed; ReclaimedBytes=$freed; TemporaryFilesRemaining=@(@($dll,$rsp) | Where-Object {Test-Path -LiteralPath $_}).Count } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
}
