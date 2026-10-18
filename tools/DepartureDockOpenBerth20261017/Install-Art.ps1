$ErrorActionPreference = 'Stop'
$dockWorkspace = 'D:\Game'
$dockGenerated = 'C:\Users\Admin\.codex\generated_images\01a14b03-5b43-76b0-82be-b3999dcce8de'
$dockDestination = Join-Path $dockWorkspace 'UnityClient\Assets\_Project\Resources\PortFacilities\Regional\Voyage'
$dockSources = [ordered]@{
    'broken_anchor' = 'exec-133bddd6-9445-4380-87a9-0b8cf5f9de15.png'
    'white_shell' = 'exec-48ec71de-b38b-4788-a0ca-a186e6325305.png'
    'bronze_bell' = 'exec-fee26dd1-e70c-433b-a7e3-c4cc256e7f12.png'
    'salt_crown' = 'exec-53629e85-547e-4a34-ba02-a56b318f061e.png'
    'twilight_tide' = 'exec-f28ed374-61da-48ea-a44c-7d84a5480135.png'
    'gray_gull' = 'exec-d648e537-fa39-49e2-8b0d-ec710c2d0331.png'
}
Add-Type -AssemblyName System.Drawing
$dockRecords = @()
foreach ($dockEntry in $dockSources.GetEnumerator()) {
    $dockSourceFile = Join-Path $dockGenerated $dockEntry.Value
    $dockFileName = 'voyage_' + $dockEntry.Key + '_open_berth_v1.png'
    $dockTargetFile = Join-Path $dockDestination $dockFileName
    Copy-Item -LiteralPath $dockSourceFile -Destination $dockTargetFile
    $dockTemplateName = if ($dockEntry.Key -eq 'broken_anchor') { 'voyage_broken_anchor_scene_v4.png.meta' } else { 'voyage_' + $dockEntry.Key + '_scene_v1.png.meta' }
    $dockMeta = Get-Content -LiteralPath (Join-Path $dockDestination $dockTemplateName) -Raw
    $dockGuid = [guid]::NewGuid().ToString('N')
    if (Test-Path -LiteralPath ($dockTargetFile + '.meta')) {
        $dockExistingMeta = Get-Content -LiteralPath ($dockTargetFile + '.meta') -Raw
        $dockGuidMatch = [regex]::Match($dockExistingMeta, '(?m)^guid: ([a-f0-9]{32})\r?$')
        if (-not $dockGuidMatch.Success) { throw ('Existing asset has an invalid GUID: ' + $dockTargetFile) }
        $dockGuid = $dockGuidMatch.Groups[1].Value
    }
    $dockMeta = $dockMeta -replace '(?m)^guid: [a-f0-9]+', ('guid: ' + $dockGuid)
    [IO.File]::WriteAllText($dockTargetFile + '.meta', $dockMeta, [Text.UTF8Encoding]::new($false))
    $dockBitmap = [Drawing.Image]::FromFile($dockTargetFile)
    $dockRecords += [pscustomobject]@{ port = $dockEntry.Key; file = $dockFileName; width = $dockBitmap.Width; height = $dockBitmap.Height; sha256 = (Get-FileHash -LiteralPath $dockTargetFile -Algorithm SHA256).Hash; source = $dockSourceFile; tool = 'built-in image_gen'; }
    $dockBitmap.Dispose()
}
$dockRecords | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $dockWorkspace 'Artifacts\DepartureDockOpenBerth20261017\art-manifest.json') -Encoding UTF8
$dockRecords | Format-Table port,width,height,file -AutoSize

