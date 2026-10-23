$ErrorActionPreference = 'Stop'
$reviewUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$reviewRoot = 'D:/Game/Artifacts/CardArtReview20261022/RoutingAudit'
New-Item -ItemType Directory -Force -Path $reviewRoot | Out-Null
$reviewLoader = Get-Content -LiteralPath 'D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs' -Raw
$reviewStart = $reviewLoader.IndexOf('        public static bool HasSchoolIllustration(')
$reviewEnd = $reviewLoader.IndexOf('        public static BattleCardPalette Palette(', $reviewStart)
if ($reviewStart -lt 0 -or $reviewEnd -lt 0) { throw 'Cannot locate production illustration methods' }
$reviewLoad = [regex]::Match($reviewLoader, '(?s)        private static Texture2D Load\(string path\).*?(?=        private static BattleCardPalette Make)').Value
$reviewExtracted = "using System; using System.Collections.Generic; using ShatteredSail.Core.Gameplay; using UnityEngine; namespace ShatteredSail.Presentation { public static class CardSchoolArtCatalog { private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);`n" + $reviewLoader.Substring($reviewStart, $reviewEnd - $reviewStart) + $reviewLoad + '} }'
$reviewExtractedPath = Join-Path $reviewRoot 'ProductionIllustrationMethods.cs'
[IO.File]::WriteAllText($reviewExtractedPath, $reviewExtracted)
$reviewGenerated = @()
foreach ($reviewManifest in @(Get-ChildItem -LiteralPath 'D:/Game/Artifacts/CardArtReview20261022' -Filter '*generated.json')) {
    $reviewRows = @(Get-Content -LiteralPath $reviewManifest.FullName -Raw | ConvertFrom-Json)
    foreach ($reviewRow in $reviewRows) {
        if ($reviewRow.id) { $reviewGenerated += $reviewRow.id }
        elseif ($reviewRow.cardId) { $reviewGenerated += $reviewRow.cardId }
    }
}
$reviewGeneratedPath = Join-Path $reviewRoot 'new-card-ids.txt'
[IO.File]::WriteAllLines($reviewGeneratedPath, @($reviewGenerated | Sort-Object -Unique))
$reviewExe = Join-Path $reviewRoot 'ArtRoutingAudit.exe'
$reviewArgs = @((Join-Path $reviewUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$reviewExe")
foreach ($reviewAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $reviewArgs += '-r:' + (Join-Path $reviewUnity ('MonoBleedingEdge/lib/mono/4.7.2-api/' + $reviewAssembly))
}
$reviewArgs += @(Get-ChildItem -LiteralPath 'D:/Game/UnityClient/Assets/_Project/Core/Runtime' -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$reviewArgs += $reviewExtractedPath
$reviewArgs += Join-Path $PSScriptRoot 'ArtRoutingAudit.cs'
& (Join-Path $reviewUnity 'NetCoreRuntime/dotnet.exe') @reviewArgs
if ($LASTEXITCODE -ne 0) { throw 'Art routing audit compilation failed' }
& (Join-Path $reviewUnity 'MonoBleedingEdge/bin/mono.exe') $reviewExe 'D:/Game/UnityClient/Assets/_Project/Resources' $reviewRoot $reviewGeneratedPath
if ($LASTEXITCODE -ne 0) { throw 'Art routing audit failed' }
