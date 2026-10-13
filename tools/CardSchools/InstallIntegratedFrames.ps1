param(
    [string]$GeneratedDirectory = 'D:\Game\Artifacts\CardFramesIntegrated20261012\Generated',
    [string]$TargetDirectory = 'D:\Game\UnityClient\Assets\_Project\Resources\CardSchools\Frames\Integrated'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$utf8 = [System.Text.UTF8Encoding]::new($false)
$template = [IO.File]::ReadAllText((Join-Path $TargetDirectory 'frame-fire-both.png.meta'))
$selected = Get-ChildItem -LiteralPath $GeneratedDirectory -Filter 'frame-*.json' | Where-Object { $_.Name -notmatch '-rejected-' } | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
} | Group-Object id | ForEach-Object { $_.Group | Sort-Object -Property version -Descending | Select-Object -First 1 } | Sort-Object id
$records = @()
# Validate the entire batch before changing any production file.
foreach ($record in $selected) {
    if ($record.id -notmatch '^frame-(neutral|fire|arcane|curse|tide|thunder|blood)-(ap|cannonball|powder|both)(-(mana|health|curse|chain))?$') { throw "Unexpected frame id: $($record.id)" }
    $probe = [Drawing.Bitmap]::new($record.source)
    try {
        if ($record.version -ge 4 -and ($probe.Width -ne 965 -or $probe.Height -ne 1630)) { throw "Unexpected tall frame dimensions: $($record.id)" }
        if ($probe.GetPixel([int]($probe.Width/2),[int]($probe.Height*.3)).A -gt 2 -or $probe.GetPixel(0,0).A -gt 2) { throw "Non-transparent illustration opening or exterior: $($record.id)" }
        if ($record.version -ge 3 -and $probe.GetPixel([int]($probe.Width/2),[int]($probe.Height*.7)).A -lt 250) { throw "Missing opaque parchment: $($record.id)" }
    } finally { $probe.Dispose() }
}
foreach ($record in $selected) {
    if ($record.id -notmatch '^frame-(neutral|fire|arcane|curse|tide|thunder|blood)-(ap|cannonball|powder|both)(-(mana|health|curse|chain))?$') { throw "Unexpected frame id: $($record.id)" }
    $target = Join-Path $TargetDirectory ($record.id + '.png')
    $bitmap = [Drawing.Bitmap]::new($record.source)
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
        $centerAlpha = $bitmap.GetPixel([int]($width / 2), [int]($height / 2)).A
        $artWindowAlpha = $bitmap.GetPixel([int]($width / 2), [int]($height * 0.3)).A
        $paperAlpha = $bitmap.GetPixel([int]($width / 2), [int]($height * 0.7)).A
        $cornerAlpha = $bitmap.GetPixel(0, 0).A
        # A one-step alpha fringe from generated glow is visually transparent.
        if ($artWindowAlpha -gt 2 -or $cornerAlpha -gt 2) { throw "Non-transparent illustration opening or exterior: $($record.id)" }
        if ($record.version -ge 3 -and $paperAlpha -lt 250) { throw "Missing integrated opaque parchment: $($record.id)" }
    } finally { $bitmap.Dispose() }
    $hash = (Get-FileHash -LiteralPath $record.source -Algorithm SHA256).Hash
    if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) {
        Copy-Item -LiteralPath $record.source -Destination $target
    }
    if (-not (Test-Path -LiteralPath ($target + '.meta'))) {
        $meta = [regex]::Replace($template, '(?m)^guid: [a-f0-9]+', ('guid: ' + [Guid]::NewGuid().ToString('N')))
        [IO.File]::WriteAllText(($target + '.meta'), $meta, $utf8)
    }
    $record.target = $target
    $record | Add-Member -NotePropertyName width -NotePropertyValue $width -Force
    $record | Add-Member -NotePropertyName height -NotePropertyValue $height -Force
    $record | Add-Member -NotePropertyName sha256 -NotePropertyValue $hash -Force
    $record | Add-Member -NotePropertyName alphaValidated -NotePropertyValue $true -Force
    $record | Add-Member -NotePropertyName centerAlpha -NotePropertyValue $centerAlpha -Force
    $record | Add-Member -NotePropertyName artWindowAlpha -NotePropertyValue $artWindowAlpha -Force
    $record | Add-Member -NotePropertyName paperAlpha -NotePropertyValue $paperAlpha -Force
    $record | Add-Member -NotePropertyName topLeftAlpha -NotePropertyValue $cornerAlpha -Force
    $records += $record
}
$ledger = Join-Path (Split-Path $GeneratedDirectory -Parent) 'generated-integrated-frames.json'
[IO.File]::WriteAllText($ledger, (ConvertTo-Json -InputObject $records -Depth 12), $utf8)
Write-Output ("Installed {0} integrated frames. Ledger: {1}" -f $records.Count, $ledger)
$records | Select-Object id, width, height | Format-Table -AutoSize
