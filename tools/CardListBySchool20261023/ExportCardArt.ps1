param(
    [string]$Output = 'D:/Game/outputs/card-list-by-school-20261023',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [ValidateRange(128,2048)][int]$MaximumEdge = 640,
    [string]$Python = 'C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
)
$ErrorActionPreference = 'Stop'
$artWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artOutput = [IO.Path]::GetFullPath($Output)
$artResourceRoot = Join-Path $artWorkspace 'UnityClient/Assets/_Project/Resources'
$artCatalogPath = Join-Path $artWorkspace 'UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs'
$artBasePath = Join-Path $artOutput 'card-list-data.json'
if (-not (Test-Path -LiteralPath $artBasePath -PathType Leaf)) { throw 'Export current base card catalogue first.' }
$artBuild = Join-Path $artOutput ('ArtBuild-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $artBuild | Out-Null
$artCompleted = $false
$artReleased = 0L
try {
    # Compile the current production selection methods verbatim, replacing only
    # Unity's texture existence query with the current on-disk resource index.
    $artSource = [IO.File]::ReadAllText($artCatalogPath)
    $artStart = $artSource.IndexOf('        public static bool HasSchoolIllustration(CoreBattleCard card)')
    $artEnd = $artSource.IndexOf('        public static bool HasDedicatedArt(CoreBattleCard card)')
    if ($artStart -lt 0 -or $artEnd -le $artStart) { throw 'Production card-art method boundaries changed; review the exporter.' }
    $artMethods = $artSource.Substring($artStart, $artEnd - $artStart)
    foreach ($artMethod in @('HasSchoolIllustration','WesternIllustrationPath','IllustrationIdentity','ReviewedIllustrationPath','DedicatedIllustrationPath')) {
        if (-not $artMethods.Contains($artMethod + '(')) { throw ('Missing runtime art method: ' + $artMethod) }
    }
    $artTemplate = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ExportCardArt.cs.template'))
    $artGenerated = Join-Path $artBuild 'ExportCardArt.cs'
    [IO.File]::WriteAllText($artGenerated, $artTemplate.Replace('/* CURRENT_RUNTIME_ART_METHODS */', $artMethods), [Text.UTF8Encoding]::new($false))
    $artSources = @(Get-ChildItem -LiteralPath (Join-Path $artWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -Recurse | Sort-Object FullName)
    $artTracked = @((Join-Path $PSScriptRoot 'BuildCardArtThumbnails.py')) + @($artSources.FullName) + @($artCatalogPath, $artBasePath, (Join-Path $PSScriptRoot 'ExportCardArt.cs.template'), $PSCommandPath)
    $artHashes = @($artTracked | ForEach-Object { [pscustomobject]@{Source=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} })
    $artHashes | Export-Csv -LiteralPath (Join-Path $artOutput 'art-source-hashes.csv') -NoTypeInformation -Encoding utf8
    $artFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
    $artExe = Join-Path $artBuild 'ExportCardArt.exe'
    $artArguments = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',('-out:' + $artExe))
    foreach ($artAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll','System.Drawing.dll')) {
        $artArguments += '-r:' + (Join-Path $artFramework $artAssembly)
    }
    $artArguments += $artSources.FullName
    $artArguments += $artGenerated
    & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @artArguments 2>&1 | Tee-Object -FilePath (Join-Path $artOutput 'art-compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'Card art exporter compilation failed.' }
    & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $artExe $artOutput $artResourceRoot $artBasePath $MaximumEdge 2>&1 | Tee-Object -FilePath (Join-Path $artOutput 'art-export.log')
    if ($LASTEXITCODE -ne 0) { throw 'Card art export failed.' }
    & $Python (Join-Path $PSScriptRoot 'BuildCardArtThumbnails.py') --manifest (Join-Path $artOutput 'card-art-data.json')
    if ($LASTEXITCODE -ne 0) { throw 'Card art thumbnail generation or verification failed.' }
    foreach ($artHash in $artHashes) {
        if ((Get-FileHash -LiteralPath $artHash.Source -Algorithm SHA256).Hash -ne $artHash.SHA256) { throw ('Source changed during art export: ' + $artHash.Source) }
    }
    $artCompleted = $true
}
finally {
    # This invocation owns only the unique ArtBuild directory. Preserve logs,
    # manifests and final thumbnails; never touch any UnityClient directory.
    $artResolvedOutput = [IO.Path]::GetFullPath($artOutput).TrimEnd('\','/')
    $artResolvedBuild = [IO.Path]::GetFullPath($artBuild)
    if (-not $artResolvedBuild.StartsWith($artResolvedOutput + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($artResolvedBuild) -notmatch '^ArtBuild-[a-f0-9]{32}$' -or
        $artResolvedBuild.StartsWith((Join-Path $artWorkspace 'UnityClient'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe build cleanup path.' }
    if (Test-Path -LiteralPath $artResolvedBuild) {
        $artBuildItem = Get-Item -LiteralPath $artResolvedBuild
        $artBuildChildren = @(Get-ChildItem -LiteralPath $artResolvedBuild -Recurse -Force)
        if (($artBuildItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or @($artBuildChildren | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Refusing cleanup through directory links.' }
        $artReleased = ($artBuildChildren | Where-Object { -not $_.PSIsContainer } | Measure-Object Length -Sum).Sum
        Remove-Item -LiteralPath $artResolvedBuild -Recurse -Force
        if (Test-Path -LiteralPath $artResolvedBuild) { throw 'Art build cleanup failed.' }
    }
    [pscustomobject]@{ Success=$artCompleted; RemovedBuild=$artResolvedBuild; ReleasedBytes=$artReleased; MainProjectModified=$false; Retained='Final art thumbnails, art manifest, source hashes, validation and logs.' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artOutput 'art-cleanup.json') -Encoding utf8
}
