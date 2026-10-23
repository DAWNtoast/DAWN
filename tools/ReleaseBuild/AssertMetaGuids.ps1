param(
    [Parameter(Mandatory=$true)][string]$AssetsPath
)
$ErrorActionPreference = 'Stop'
$assets = [IO.Path]::GetFullPath($AssetsPath)
if (-not (Test-Path -LiteralPath $assets -PathType Container)) { throw "Assets directory is missing: $assets" }
$owners = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
$problems = [Collections.Generic.List[string]]::new()
foreach ($file in Get-ChildItem -LiteralPath $assets -Filter '*.meta' -File -Recurse -Force | Sort-Object FullName) {
    $lines = [regex]::Matches([IO.File]::ReadAllText($file.FullName), '(?m)^[ \t]*guid[ \t]*:[^\r\n]*')
    if ($lines.Count -ne 1) {
        $problems.Add("$($file.FullName): expected exactly one guid field; found $($lines.Count).")
        continue
    }
    $match = [regex]::Match($lines[0].Value, '^[ \t]*guid[ \t]*:[ \t]*([0-9a-fA-F]{32})[ \t]*$')
    if (-not $match.Success) {
        $problems.Add("$($file.FullName): guid must contain exactly 32 hexadecimal characters.")
        continue
    }
    $guid = $match.Groups[1].Value
    if ($owners.ContainsKey($guid)) {
        $problems.Add("$($file.FullName): duplicate guid $guid; also used by $($owners[$guid]).")
    } else {
        $owners.Add($guid, $file.FullName)
    }
}
if ($problems.Count) { throw ("Asset meta GUID validation failed:`n" + ($problems -join "`n")) }
