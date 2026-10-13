param([string]$ArtifactDirectory = 'D:\Game\Artifacts\CardFramesIntegrated20261012')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$frameDirectory = 'D:\Game\UnityClient\Assets\_Project\Resources\CardSchools\Frames\Integrated'
$records = @(Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'generated-integrated-frames.json') -Raw | ConvertFrom-Json)
$layouts = (Get-Content -LiteralPath (Join-Path $frameDirectory 'frame-layouts.json') -Raw | ConvertFrom-Json).entries
$issues = [Collections.Generic.List[string]]::new()
$samples = @()
$guids = @()
foreach ($record in $records) {
    if ($record.version -lt 3) { $issues.Add("Old paperless version: $($record.id)") }
    if ((Get-FileHash -LiteralPath $record.source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $record.target -Algorithm SHA256).Hash) { $issues.Add("Source mismatch: $($record.id)") }
    $meta = Get-Content -LiteralPath ($record.target + '.meta') -Raw
    $guids += [regex]::Match($meta, '(?m)^guid: ([a-f0-9]+)').Groups[1].Value
    $bitmap = [Drawing.Bitmap]::new($record.target)
    try {
        if ($record.version -ge 4) {
            if ($bitmap.Width -ne 965 -or $bitmap.Height -ne 1630) { $issues.Add("Unexpected dimensions: $($record.id)") }
            $window = @(@(300,350),@(480,500),@(700,750)) | ForEach-Object { $bitmap.GetPixel($_[0],$_[1]).A }
            $paper = @(@(350,1120),@(480,1220),@(700,1320)) | ForEach-Object { $bitmap.GetPixel($_[0],$_[1]).A }
            $name = $bitmap.GetPixel(480,975).A
        } else {
            if ($bitmap.Width -ne 1078 -or $bitmap.Height -ne 1459) { $issues.Add("Unexpected dimensions: $($record.id)") }
            $window = @(@(300,350),@(540,400),@(800,550)) | ForEach-Object { $bitmap.GetPixel($_[0],$_[1]).A }
            $paper = @(@(350,950),@(540,1050),@(720,1120)) | ForEach-Object { $bitmap.GetPixel($_[0],$_[1]).A }
            $name = $bitmap.GetPixel(540,795).A
        }
        $exterior = $bitmap.GetPixel(0,0).A
        if (@($window | Where-Object {$_ -gt 2}).Count -gt 0) { $issues.Add("Illustration window not transparent: $($record.id)") }
        if (@($paper | Where-Object {$_ -lt 250}).Count -gt 0 -or $name -lt 250) { $issues.Add("Integrated paper/name missing: $($record.id)") }
        if ($exterior -gt 2) { $issues.Add("Exterior not transparent: $($record.id)") }
        $samples += [pscustomobject]@{Id=$record.id;Version=$record.version;ArtWindowAlpha=$window;PaperAlpha=$paper;NameAlpha=$name;ExteriorAlpha=$exterior}
    } finally { $bitmap.Dispose() }
    if (@($layouts | Where-Object id -eq $record.id).Count -ne 1) { $issues.Add("Missing or duplicate layout: $($record.id)") }
}
$pngCount = @(Get-ChildItem -LiteralPath $frameDirectory -Filter 'frame-*.png').Count
$uniqueGuidCount = @($guids | Sort-Object -Unique).Count
if ($records.Count -ne 36 -or $pngCount -ne 36 -or $uniqueGuidCount -ne 36 -or $layouts.Count -ne 36) { $issues.Add('Expected 36 records, PNGs, unique import GUIDs and layout entries.') }
$report = [pscustomobject]@{GeneratedAt=[DateTimeOffset]::Now.ToString('o');Expected=36;Selected=$records.Count;PngCount=$pngCount;UniqueMetaGuids=$uniqueGuidCount;LayoutEntries=$layouts.Count;SourceAndTargetHashesMatch=(@($issues | Where-Object {$_ -like 'Source mismatch:*'}).Count -eq 0);Passed=($issues.Count -eq 0);Errors=@($issues);Samples=$samples}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'asset-install-validation.json') -Encoding utf8
$report | Select-Object Expected,Selected,PngCount,UniqueMetaGuids,LayoutEntries,Passed,Errors | ConvertTo-Json -Depth 4
if ($issues.Count -gt 0) { throw 'Integrated frame asset validation failed.' }
