$ErrorActionPreference = 'Stop'
$root = 'D:\Game'
$out = Join-Path $root 'outputs\battle-icon-reachability-20261026'
$snapshot = Join-Path $out 'source-snapshot'
if (Test-Path -LiteralPath (Join-Path $out 'source-manifest.json')) { throw 'Preserve existing audit snapshot.' }
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
$files = foreach ($area in @('Core','Content','Presentation')) {
    Get-ChildItem -LiteralPath (Join-Path $root ('UnityClient\Assets\_Project\'+$area+'\Runtime')) -File -Recurse |
        Where-Object { $_.Extension -in @('.cs','.json','.asmdef') }
}
$records = foreach ($file in $files) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked input.' }
    $relative = $file.FullName.Substring($root.Length+1)
    $target = Join-Path $snapshot $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    [IO.File]::WriteAllBytes($target,$bytes)
    [ordered]@{relative=$relative; source=$file.FullName; snapshot=$target; sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash; bytes=$bytes.Length}
}
$old = Join-Path $out 'previous-inventory'
New-Item -ItemType Directory -Path $old -Force | Out-Null
foreach ($name in @('controls.json','statuses.json','other.json','keywords.json','all-icons.json','战斗图标与悬停提示.html','战斗图标与悬停提示.md','build-report.cjs','check-report.cjs','source-manifest.json')) {
    Copy-Item -LiteralPath (Join-Path $root ('outputs\battle-icon-tooltips-20261026\'+$name)) -Destination (Join-Path $old $name)
}
Copy-Item -LiteralPath (Join-Path $root 'UnityClient\Assets\_Project\Resources\BattleIcons\battle_icons_v1.png') -Destination (Join-Path $out 'battle_icons_v1.png')
[ordered]@{capturedAtUtc=[DateTime]::UtcNow.ToString('O'); files=@($records); sourceFiles=@($records).Count; totalBytes=($records | Measure-Object -Property bytes -Sum).Sum} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'source-manifest.json') -Encoding utf8
Write-Output ('Frozen source files: '+@($records).Count)
