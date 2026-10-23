$ErrorActionPreference = 'Stop'
$uniqueUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$uniqueBase = 'D:/Game/Artifacts/CardArtUnique20261022'
$uniqueOutput = Join-Path $uniqueBase 'RoutingAudit'
New-Item -ItemType Directory -Force -Path $uniqueOutput | Out-Null
$uniqueScope = @(Get-Content -LiteralPath (Join-Path $uniqueBase 'scope.json') -Raw | ConvertFrom-Json)
if ($uniqueScope.Count -ne 38 -or @($uniqueScope.id | Sort-Object -Unique).Count -ne 38) { throw 'Scope must have exactly 38 unique IDs' }
$uniqueInstalledPath = Join-Path $uniqueBase 'installed.json'
$uniqueInstalled = if (Test-Path -LiteralPath $uniqueInstalledPath) { @(Get-Content -LiteralPath $uniqueInstalledPath -Raw | ConvertFrom-Json) } else { @() }
if (@($uniqueInstalled.id | Sort-Object -Unique).Count -ne $uniqueInstalled.Count) { throw 'installed.json contains duplicate IDs' }
foreach ($uniqueRow in $uniqueInstalled) {
    if ($uniqueRow.id -notin $uniqueScope.id) { throw ('Installed card outside scope: ' + $uniqueRow.id) }
}
$uniqueInput = @($uniqueScope | ForEach-Object {
    $uniqueCardId = $_.id
    $uniqueMatch = @($uniqueInstalled | Where-Object id -eq $uniqueCardId)
    $uniqueFlag = if ($uniqueMatch.Count -eq 1) { '1' } else { '0' }
    $uniqueHash = if ($uniqueMatch.Count -eq 1) { $uniqueMatch[0].sha256 } else { '' }
    @($_.id, $_.name, $_.before, $_.beforeSha256, $uniqueFlag, $uniqueHash) -join "`t"
})
$uniqueInputPath = Join-Path $uniqueOutput 'scope-installed-input.tsv'
[IO.File]::WriteAllLines($uniqueInputPath, $uniqueInput, [Text.UTF8Encoding]::new($false))
$uniqueLoaderPath = 'D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs'
$uniqueLoader = Get-Content -LiteralPath $uniqueLoaderPath -Raw
$uniqueStart = $uniqueLoader.IndexOf('        public static bool HasSchoolIllustration(')
$uniqueEnd = $uniqueLoader.IndexOf('        public static BattleCardPalette Palette(', $uniqueStart)
if ($uniqueStart -lt 0 -or $uniqueEnd -lt 0) { throw 'Cannot locate production illustration methods' }
$uniqueLoad = [regex]::Match($uniqueLoader, '(?s)        private static Texture2D Load\(string path\).*?(?=        private static BattleCardPalette Make)').Value
if ($uniqueLoad.Length -eq 0) { throw 'Cannot extract production texture loader' }
$uniqueExtracted = "using System; using System.Collections.Generic; using ShatteredSail.Core.Gameplay; using UnityEngine; namespace ShatteredSail.Presentation { public static class CardSchoolArtCatalog { private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);`n" + $uniqueLoader.Substring($uniqueStart, $uniqueEnd - $uniqueStart) + $uniqueLoad + '} }'
$uniqueExtractedPath = Join-Path $uniqueOutput 'ProductionIllustrationMethods.cs'
[IO.File]::WriteAllText($uniqueExtractedPath, $uniqueExtracted)
$uniqueCore = @(Get-ChildItem -LiteralPath 'D:/Game/UnityClient/Assets/_Project/Core/Runtime' -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$uniqueHashes = @(@($uniqueLoaderPath) + $uniqueCore | ForEach-Object { [pscustomobject]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
$uniqueHashes | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $uniqueOutput 'production-source-hashes.json') -Encoding utf8
$uniqueExe = Join-Path $uniqueOutput 'ArtRoutingAudit.exe'
$uniqueArgs = @((Join-Path $uniqueUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$uniqueExe")
foreach ($uniqueAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $uniqueArgs += '-r:' + (Join-Path $uniqueUnity ('MonoBleedingEdge/lib/mono/4.7.2-api/' + $uniqueAssembly))
}
$uniqueArgs += $uniqueCore
$uniqueArgs += $uniqueExtractedPath
$uniqueArgs += Join-Path $PSScriptRoot 'ArtRoutingAudit.cs'
& (Join-Path $uniqueUnity 'NetCoreRuntime/dotnet.exe') @uniqueArgs
if ($LASTEXITCODE -ne 0) { throw 'Independent card-art routing audit compilation failed' }
foreach ($uniqueHashRow in $uniqueHashes) {
    if ((Get-FileHash -LiteralPath $uniqueHashRow.path -Algorithm SHA256).Hash -ne $uniqueHashRow.sha256) { throw ('Production source changed during compilation: ' + $uniqueHashRow.path) }
}
& (Join-Path $uniqueUnity 'MonoBleedingEdge/bin/mono.exe') $uniqueExe 'D:/Game/UnityClient/Assets/_Project/Resources' $uniqueOutput $uniqueInputPath
if ($LASTEXITCODE -ne 0) { throw 'Independent card-art routing audit failed; see RoutingAudit/art-routing-audit.json' }
