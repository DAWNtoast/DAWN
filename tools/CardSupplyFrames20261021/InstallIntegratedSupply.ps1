$ErrorActionPreference = 'Stop'
$root = 'D:\Game'
$generated = 'C:\Users\Admin\.codex\generated_images\01a15f47-1c2a-7483-b61d-0d6d2e60eaeb'
$target = Join-Path $root 'UnityClient\Assets\_Project\Resources\CardSchools\Frames\Integrated'
$output = Join-Path $root 'outputs\imagegen\card-five-cost-frame-20261021'
$variants = @(
    @{ Base='neutral-ap'; Source='exec-24b56922-3621-4549-b044-6f39bb24aef4.png'; X=882; Y=430; Height=1630 },
    @{ Base='fire-powder'; Source='exec-47906415-4ed1-433e-9095-13441dfb6586.png'; X=880; Y=389; Height=1630 },
    @{ Base='arcane-ap-mana'; Source='exec-165ae672-546e-4bc4-909a-52ce91f327e9.png'; X=874; Y=479; Height=1630 },
    @{ Base='curse-ap-curse'; Source='exec-570f3478-6e10-49d0-b63f-edd9f24b38f5.png'; X=870; Y=477; Height=1630 },
    @{ Base='tide-ap'; Source='exec-0e6dc401-763b-4449-8ef2-2ecfd47eb270.png'; X=878; Y=413; Height=1630 },
    @{ Base='blood-ap'; Source='exec-931a00f7-4a9e-4fed-b7b8-eac35319691a.png'; X=873; Y=412; Height=1629 },
    @{ Base='fire-ap'; Id='frame-fire-ap-health'; Source='exec-7a0b0bfe-a3cf-4c53-83b4-8ccc3105fb09.png' },
    @{ Base='tide-ap'; Id='frame-tide-ap-tide'; Source='exec-27764377-f5a4-4a11-a56c-a7226eaca3a9.png' },
    @{ Base='tide-both'; Id='frame-tide-both-tide'; Source='exec-37eb1847-03aa-46b8-b6d7-1bb899bdf9d0.png' },
    @{ Base='tide-ap'; Id='frame-tide-ap-tide-supply'; Source='exec-4c31cac8-7445-489c-aed8-768effc39c1c.png'; X=878; Y=413; Height=1630 }
)
$layoutPath = Join-Path $target 'frame-layouts.json'
$layoutText = [IO.File]::ReadAllText($layoutPath)
$layout = $layoutText | ConvertFrom-Json
$newEntries = @()
foreach ($variant in $variants) {
    $baseId = 'frame-' + $variant.Base
    $id = $baseId + '-supply'
    if ($variant.ContainsKey('Id')) { $id = $variant.Id }
    Copy-Item -LiteralPath (Join-Path $generated $variant.Source) -Destination (Join-Path $target ($id + '.png'))
    Copy-Item -LiteralPath (Join-Path $generated $variant.Source) -Destination (Join-Path $output ($id + '.png'))
    $metaPath = Join-Path $target ($id + '.png.meta')
    if (-not (Test-Path -LiteralPath $metaPath)) {
        $meta = [IO.File]::ReadAllText((Join-Path $target ($baseId + '.png.meta')))
        $meta = $meta -replace 'guid: [a-f0-9]{32}', ('guid: ' + [Guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($metaPath, $meta)
    }
    if (-not ($layout.entries | Where-Object { $_.id -eq $id })) {
        $baseEntry = $layout.entries | Where-Object { $_.id -eq $baseId } | Select-Object -First 1
        if ($null -eq $baseEntry) { throw "Missing calibration: $baseId" }
        $entry = $baseEntry | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        $entry.id = $id
        if ($variant.ContainsKey('X')) {
            $point = [ordered]@{
                x=[Math]::Round(($entry.rect.x + $variant.X / 965.0 * $entry.rect.width), 3)
                y=[Math]::Round(($entry.rect.y + $entry.rect.height - $variant.Y / $variant.Height * $entry.rect.height), 3)
            }
            $entry | Add-Member -NotePropertyName supplyPosition -NotePropertyValue $point
        }
        $newEntries += ($entry | ConvertTo-Json -Depth 12)
    }
}
if ($newEntries.Count -gt 0) {
    $closing = $layoutText.LastIndexOf(']')
    $prefix = $layoutText.Substring(0, $closing).TrimEnd()
    [IO.File]::WriteAllText($layoutPath, $prefix + ",`r`n" + ($newEntries -join ",`r`n") + "`r`n  " + $layoutText.Substring($closing))
}
Write-Output ("Installed " + $variants.Count + " integrated supply frames.")
