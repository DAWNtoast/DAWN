param([ValidateSet('Install','Inspect')][string]$Stage = 'Install')
$ErrorActionPreference = 'Stop'
$supplyRoot = 'D:\Game'
$supplyTarget = Join-Path $supplyRoot 'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated'
$supplyOutput = Join-Path $supplyRoot 'outputs/imagegen/card-supply-integrated-20261022'
$supplyManifest = Get-Content -LiteralPath (Join-Path $supplyOutput 'generation-manifest.json') -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
if ($supplyManifest.Count -ne 8) { throw 'Exactly eight audited new frames are required.' }
$supplyLayoutPath = Join-Path $supplyTarget 'frame-layouts.json'
$supplyLayoutText = [IO.File]::ReadAllText($supplyLayoutPath)
$supplyLayouts = $supplyLayoutText | ConvertFrom-Json
$supplyNewEntries = [Collections.Generic.List[object]]::new()
$supplyEvidence = [Collections.Generic.List[object]]::new()
foreach ($supplyItem in $supplyManifest) {
    $supplySource = [IO.Path]::GetFullPath($supplyItem.source)
    $supplyAsset = Join-Path $supplyTarget ($supplyItem.id + '.png')
    if (-not (Test-Path -LiteralPath $supplySource)) { throw ('Missing generated source: ' + $supplyItem.id) }
    $supplyImage = [Drawing.Bitmap]::FromFile($supplySource)
    try {
        $supplyWidth = $supplyImage.Width
        $supplyHeight = $supplyImage.Height
        $supplyAlphaOutside = $supplyImage.GetPixel(0,0).A
        $supplyAlphaWindow = $supplyImage.GetPixel([int]($supplyWidth/2),[int]($supplyHeight/3)).A
        $supplyAlphaPaper = $supplyImage.GetPixel([int]($supplyWidth/2),[int]($supplyHeight*0.75)).A
        $supplyAlphaIcon = $supplyImage.GetPixel([int]$supplyItem.x,[int]$supplyItem.y).A
        if ($supplyWidth -ne 965 -or $supplyHeight -lt 1629 -or $supplyHeight -gt 1630) { throw ('Unexpected dimensions: ' + $supplyItem.id) }
        if ($supplyAlphaOutside -gt 1 -or $supplyAlphaWindow -gt 1 -or $supplyAlphaPaper -lt 250 -or $supplyAlphaIcon -lt 250) { throw ('Invalid transparent/opaque regions: ' + $supplyItem.id + ' outside/window/paper/icon = ' + $supplyAlphaOutside + '/' + $supplyAlphaWindow + '/' + $supplyAlphaPaper + '/' + $supplyAlphaIcon) }
    } finally { $supplyImage.Dispose() }
    if ($Stage -eq 'Install') {
        Copy-Item -LiteralPath $supplySource -Destination $supplyAsset -Force
        Copy-Item -LiteralPath $supplySource -Destination (Join-Path $supplyOutput ($supplyItem.id + '.png')) -Force
        if (-not (Test-Path -LiteralPath ($supplyAsset + '.meta'))) {
            $supplyMeta = [IO.File]::ReadAllText((Join-Path $supplyTarget ($supplyItem.base + '.png.meta')))
            $supplyMeta = $supplyMeta -replace 'guid: [a-f0-9]{32}', ('guid: ' + [Guid]::NewGuid().ToString('N'))
            [IO.File]::WriteAllText(($supplyAsset + '.meta'), $supplyMeta)
        }
        $supplyExisting = @($supplyLayouts.entries | Where-Object { $_.id -eq $supplyItem.id })
        if ($supplyExisting.Count -eq 0) {
            $supplyEntry = ($supplyLayouts.entries | Where-Object { $_.id -eq $supplyItem.base } | Select-Object -First 1) | ConvertTo-Json -Depth 12 | ConvertFrom-Json
            if (-not $supplyEntry) { throw ('Missing source layout: ' + $supplyItem.base) }
            $supplyEntry.id = $supplyItem.id
            $supplyPosition = [ordered]@{
                x = [Math]::Round(($supplyEntry.rect.x + $supplyItem.x / $supplyWidth * $supplyEntry.rect.width),3)
                y = [Math]::Round(($supplyEntry.rect.y + $supplyEntry.rect.height - $supplyItem.y / $supplyHeight * $supplyEntry.rect.height),3)
            }
            $supplyEntry | Add-Member -NotePropertyName supplyPosition -NotePropertyValue $supplyPosition -Force
            $supplyNewEntries.Add($supplyEntry)
        }
    }
    $supplySourceHash = (Get-FileHash -LiteralPath $supplySource).Hash
    $supplyInstalledHash = (Get-FileHash -LiteralPath $supplyAsset).Hash
    if ($supplySourceHash -ne $supplyInstalledHash) { throw ('Installed PNG differs from generated source: ' + $supplyItem.id) }
    $supplyEvidence.Add([ordered]@{id=$supplyItem.id;width=$supplyWidth;height=$supplyHeight;exteriorAlpha=$supplyAlphaOutside;windowAlpha=$supplyAlphaWindow;paperAlpha=$supplyAlphaPaper;iconAlpha=$supplyAlphaIcon;iconCenter=@{x=$supplyItem.x;y=$supplyItem.y};sourceHash=$supplySourceHash;installedHash=$supplyInstalledHash})
}
if ($Stage -eq 'Install' -and $supplyNewEntries.Count) {
    $supplyClosing = $supplyLayoutText.LastIndexOf(']')
    $supplyAddition = ($supplyNewEntries | ForEach-Object { $_ | ConvertTo-Json -Depth 12 }) -join ",`r`n"
    [IO.File]::WriteAllText($supplyLayoutPath, $supplyLayoutText.Substring(0,$supplyClosing).TrimEnd() + ",`r`n" + $supplyAddition + "`r`n  " + $supplyLayoutText.Substring($supplyClosing))
}
$supplyEvidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $supplyOutput 'asset-verification.json') -Encoding utf8
Write-Output ('Verified ' + $supplyEvidence.Count + ' complete transparent frames; source PNG pixels preserved.')
