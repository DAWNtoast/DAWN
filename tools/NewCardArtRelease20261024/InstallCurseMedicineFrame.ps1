param([Parameter(Mandatory=$true)][string]$Source)
$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$taskDirectory = Join-Path $taskWorkspace 'UnityClient/Assets/_Project/Resources/CardSchools/Frames/Integrated'
$taskOutput = Join-Path $taskWorkspace 'outputs/new-card-art-release-20261024'
$taskId = 'frame-curse-ap-medicine'
$taskBaseId = 'frame-curse-ap-curse-medicine'
$taskAsset = Join-Path $taskDirectory ($taskId + '.png')
$taskMeta = $taskAsset + '.meta'
$taskLayoutPath = Join-Path $taskDirectory 'frame-layouts.json'
$taskLayoutText = [IO.File]::ReadAllText($taskLayoutPath)
$taskLayouts = $taskLayoutText | ConvertFrom-Json
$taskOriginalEntries = @($taskLayouts.entries)
$taskBase = @($taskOriginalEntries | Where-Object id -eq $taskBaseId)
if ($taskBase.Count -ne 1) { throw 'The reviewed curse medicine frame layout is required.' }
$taskEntry = $taskBase[0] | ConvertTo-Json -Depth 12 | ConvertFrom-Json
$taskEntry.id = $taskId
$taskSourcePath = [IO.Path]::GetFullPath($Source)
$taskSourceHash = (Get-FileHash -LiteralPath $taskSourcePath).Hash
if ((Test-Path -LiteralPath $taskAsset) -and (Get-FileHash -LiteralPath $taskAsset).Hash -ne $taskSourceHash) {
    throw 'Refusing to overwrite a different installed frame.'
}
Add-Type -AssemblyName System.Drawing
$taskImage = [Drawing.Bitmap]::FromFile($taskSourcePath)
try {
    $taskWidth = $taskImage.Width; $taskHeight = $taskImage.Height
    if ($taskHeight -lt 1024 -or [Math]::Abs($taskWidth / [double]$taskHeight - 965.0/1630) -gt .004) {
        throw 'The edited frame must preserve the complete reviewed portrait aspect and source detail.'
    }
    $taskIconX = [int][Math]::Round(($taskEntry.supplyPosition.x - $taskEntry.rect.x) / $taskEntry.rect.width * $taskWidth)
    $taskIconY = [int][Math]::Round(($taskEntry.rect.y + $taskEntry.rect.height - $taskEntry.supplyPosition.y) / $taskEntry.rect.height * $taskHeight)
    $taskAlphas = [ordered]@{
        outside=$taskImage.GetPixel(0,0).A
        illustration=$taskImage.GetPixel([int]($taskWidth/2),[int]($taskHeight/3)).A
        paper=$taskImage.GetPixel([int]($taskWidth/2),[int]($taskHeight*.75)).A
        medicine=$taskImage.GetPixel($taskIconX,$taskIconY).A
    }
    if ($taskAlphas.outside -gt 1 -or $taskAlphas.illustration -gt 1 -or $taskAlphas.paper -lt 250 -or $taskAlphas.medicine -lt 250) {
        throw ('Frame transparency or existing medicine position was not preserved: ' + ($taskAlphas | ConvertTo-Json -Compress))
    }
} finally { $taskImage.Dispose() }
$taskExisting = @($taskOriginalEntries | Where-Object id -eq $taskId)
if ($taskExisting.Count -gt 1 -or ($taskExisting.Count -eq 1 -and
    ($taskExisting[0] | ConvertTo-Json -Depth 12 -Compress) -ne ($taskEntry | ConvertTo-Json -Depth 12 -Compress))) {
    throw 'A different frame layout already exists; review before replacing it.'
}
$taskTemplate = [IO.File]::ReadAllText((Join-Path $taskDirectory ($taskBaseId + '.png.meta')))
$taskTemplate = [regex]::Replace($taskTemplate,'(?m)^  compressionQuality: \d+','  compressionQuality: 100')
if ($taskTemplate -notmatch 'alphaIsTransparency: 1' -or $taskTemplate -notmatch 'enableMipMap: 0' -or
    $taskTemplate -notmatch 'buildTarget: Standalone[\s\S]*?maxTextureSize: 1024[\s\S]*?textureFormat: 25') {
    throw 'The existing integrated-frame import policy changed.'
}
if ((Test-Path -LiteralPath $taskMeta) -and -not (Test-Path -LiteralPath $taskAsset)) { throw 'Unexpected orphan frame metadata.' }
if (-not (Test-Path -LiteralPath $taskAsset)) { Copy-Item -LiteralPath $taskSourcePath -Destination $taskAsset }
if (-not (Test-Path -LiteralPath $taskMeta)) {
    $taskGuid = [Guid]::NewGuid().ToString('N')
    $taskCollision = @(Get-ChildItem -LiteralPath (Join-Path $taskWorkspace 'UnityClient/Assets') -Filter '*.meta' -File -Recurse |
        Where-Object { [IO.File]::ReadAllText($_.FullName) -match ('(?m)^guid: ' + $taskGuid + '\s*$') })
    if ($taskCollision.Count) { throw 'Generated frame GUID collided with an existing asset.' }
    [IO.File]::WriteAllText($taskMeta,[regex]::Replace($taskTemplate,'(?m)^guid: [a-fA-F0-9]{32}',('guid: ' + $taskGuid)),[Text.UTF8Encoding]::new($false))
}
if ($taskExisting.Count -eq 0) {
    $taskBackup = Join-Path $taskOutput 'frame-layouts-before-curse-medicine.json'
    if (-not (Test-Path -LiteralPath $taskBackup)) { [IO.File]::WriteAllText($taskBackup,$taskLayoutText,[Text.UTF8Encoding]::new($false)) }
    $taskClosing = $taskLayoutText.LastIndexOf(']')
    $taskAddition = $taskEntry | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($taskLayoutPath,$taskLayoutText.Substring(0,$taskClosing).TrimEnd() + ",`r`n" + $taskAddition + "`r`n  " + $taskLayoutText.Substring($taskClosing),[Text.UTF8Encoding]::new($false))
}
$taskFinalLayouts = Get-Content -LiteralPath $taskLayoutPath -Raw | ConvertFrom-Json
foreach ($entry in $taskOriginalEntries) {
    $same = @($taskFinalLayouts.entries | Where-Object id -eq $entry.id)
    if ($same.Count -ne 1 -or ($same[0] | ConvertTo-Json -Depth 12 -Compress) -ne ($entry | ConvertTo-Json -Depth 12 -Compress)) {
        throw ('An existing frame layout changed: ' + $entry.id)
    }
}
if ((Get-FileHash -LiteralPath $taskAsset).Hash -ne $taskSourceHash) { throw 'Installed frame differs from the generated source.' }
[pscustomobject]@{ Result='PASS'; id=$taskId; source=$taskSourcePath; sourceSHA256=$taskSourceHash; asset=$taskAsset;
    width=$taskWidth; height=$taskHeight; alpha=$taskAlphas; importedMaximum=1024; standaloneFormat='BC7';
    medicinePixelCenter=@{x=$taskIconX;y=$taskIconY}; supplyPosition=$taskEntry.supplyPosition;
    metaSHA256=(Get-FileHash -LiteralPath $taskMeta).Hash; layoutSHA256=(Get-FileHash -LiteralPath $taskLayoutPath).Hash;
    ExistingLayoutsUnchanged=$true } | ConvertTo-Json -Depth 7 |
    Set-Content -LiteralPath (Join-Path $taskOutput 'installed-curse-medicine-frame.json') -Encoding utf8
Write-Output 'PASS: complete curse medicine frame installed with new metadata and inherited calibrated layout; existing frames preserved.'
