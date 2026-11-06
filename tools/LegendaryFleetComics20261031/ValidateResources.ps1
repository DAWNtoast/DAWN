param()

$ErrorActionPreference = 'Stop'
$comicWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$comicTaskRoot = [IO.Path]::GetFullPath((Join-Path $comicWorkspace 'outputs/legendary-comics-20261031'))
$comicResources = [IO.Path]::GetFullPath((Join-Path $comicWorkspace 'UnityClient/Assets/_Project/Resources'))
$comicScriptPath = Join-Path $comicTaskRoot 'visual-script.json'
$comicData = Get-Content -LiteralPath $comicScriptPath -Raw -Encoding UTF8 | ConvertFrom-Json
$comicKeys = @('iron_oath', 'red_widow', 'thunder_crown', 'ember_ark', 'tide_requiem', 'last_horizon')
$comicRows = @()
$comicSeen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$comicImageHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

if ($comicData.ships.Count -ne 6) { throw 'Expected six ship scripts.' }
foreach ($comicKey in $comicKeys) {
    $comicShip = @($comicData.ships | Where-Object { $_.key -eq $comicKey })
    if ($comicShip.Count -ne 1) { throw ('Expected one script for ' + $comicKey) }
    foreach ($comicTiming in @('before', 'after')) {
        $comicPage = $comicShip[0].$comicTiming
        $comicExpected = 'LegendaryFleetComics/' + $comicKey + '_' + $comicTiming
        if ($comicPage.imageResource -cne $comicExpected -or $comicPage.panels.Count -ne 4) {
            throw ('Unexpected resource mapping or panel count: ' + $comicKey + ' ' + $comicTiming)
        }
        if (-not $comicSeen.Add($comicExpected)) { throw ('Duplicate page resource: ' + $comicExpected) }
        $comicFile = [IO.Path]::GetFullPath((Join-Path $comicResources ($comicExpected + '.png')))
        if (-not $comicFile.StartsWith($comicResources + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Image path escaped Resources.'
        }
        if (-not (Test-Path -LiteralPath $comicFile -PathType Leaf)) { throw ('Missing comic image: ' + $comicFile) }
        if (-not (Test-Path -LiteralPath ($comicFile + '.meta') -PathType Leaf)) { throw ('Missing Unity image metadata: ' + $comicFile) }
        $comicStream = [IO.File]::OpenRead($comicFile)
        try {
            $comicHeader = [byte[]]::new(24)
            if ($comicStream.Read($comicHeader, 0, $comicHeader.Length) -ne 24) { throw 'Incomplete PNG header.' }
        } finally { $comicStream.Dispose() }
        if ([BitConverter]::ToString($comicHeader, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A' -or
            [Text.Encoding]::ASCII.GetString($comicHeader, 12, 4) -ne 'IHDR') {
            throw ('Invalid PNG image: ' + $comicFile)
        }
        $comicWidth = [uint32]$comicHeader[16] * 16777216L + [uint32]$comicHeader[17] * 65536L +
            [uint32]$comicHeader[18] * 256L + [uint32]$comicHeader[19]
        $comicHeight = [uint32]$comicHeader[20] * 16777216L + [uint32]$comicHeader[21] * 65536L +
            [uint32]$comicHeader[22] * 256L + [uint32]$comicHeader[23]
        if ($comicWidth -le 0 -or $comicHeight -le 0) { throw ('Invalid image dimensions: ' + $comicFile) }
        $comicHash = (Get-FileHash -LiteralPath $comicFile -Algorithm SHA256).Hash
        if (-not $comicImageHashes.Add($comicHash)) { throw ('Two story pages reuse the same image bytes: ' + $comicFile) }
        $comicRows += [pscustomobject]@{
            EncounterId = $comicShip[0].encounterId
            Timing = $comicTiming
            Resource = $comicExpected
            File = $comicFile
            Width = $comicWidth
            Height = $comicHeight
            Bytes = (Get-Item -LiteralPath $comicFile).Length
            SHA256 = $comicHash
        }
    }
}
if ($comicRows.Count -ne 12) { throw 'Expected twelve distinct comic images.' }
$comicRows | Export-Csv -LiteralPath (Join-Path $comicTaskRoot 'resource-hashes.csv') -NoTypeInformation -Encoding utf8
@(
    'PASS six legendary ships, separate before/after pages and twelve distinct PNG resources.'
    'PASS every page has four scripted visual panels and matching Unity metadata.'
    'No Unity process was started; image dimensions and SHA256 values were read without modifying assets.'
    'Visual panel composition and absence of drawn text require the accompanying art review.'
) | Set-Content -LiteralPath (Join-Path $comicTaskRoot 'resource-validation.txt') -Encoding utf8
Write-Output ('PASS twelve comic images: ' + (Join-Path $comicTaskRoot 'resource-hashes.csv'))
