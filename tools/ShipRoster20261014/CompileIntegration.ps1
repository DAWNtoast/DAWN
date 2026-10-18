param([string]$Output = 'D:/Game/Artifacts/ShipRoster20261014/IntegrationCompile',
    [string]$ReferenceProject = 'D:/Game/tmp/第七港口v2-build-project')
$ErrorActionPreference = 'Stop'
$shipWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$shipDag = Join-Path $ReferenceProject 'Library/Bee/artifacts/1900b0aE.dag'
[IO.Directory]::CreateDirectory($Output) | Out-Null
$shipSpecs = @(
    @('Core','ShatteredSail.Core','Core/Runtime'),
    @('Content','ShatteredSail.Content','Content/Runtime'),
    @('Presentation','ShatteredSail.Presentation','Presentation/Runtime'),
    @('Edit','ShatteredSail.Core.Tests','Tests/EditMode'),
    @('Play','ShatteredSail.Presentation.PlayModeTests','Tests/PlayMode'))
foreach ($shipSpec in $shipSpecs) {
    $shipLines = @()
    foreach ($shipOriginal in Get-Content -LiteralPath (Join-Path $shipDag ($shipSpec[1] + '.rsp'))) {
        if ($shipOriginal -match '^".*\.cs"$' -or $shipOriginal -match '^[-/](out|refout):') { continue }
        $shipLine = $shipOriginal.Replace('"Library/', ('"' + $ReferenceProject + '/Library/')).Replace('"Packages/', ('"' + $ReferenceProject + '/Packages/'))
        foreach ($shipReference in @('ShatteredSail.Core','ShatteredSail.Content','ShatteredSail.Presentation')) {
            if ($shipLine -match ('^[-/]r:.*' + [regex]::Escape($shipReference) + '\.ref\.dll"$')) {
                $shipLine = '-r:"' + $Output + '/' + $shipReference + '.ref.dll"'
            }
        }
        $shipLines += $shipLine
    }
    $shipLines += '-out:"' + $Output + '/' + $shipSpec[1] + '.dll"'
    $shipLines += '-refout:"' + $Output + '/' + $shipSpec[1] + '.ref.dll"'
    $shipLines += @(Get-ChildItem -LiteralPath (Join-Path $shipWorkspace ('UnityClient/Assets/_Project/' + $shipSpec[2])) -Filter '*.cs' -Recurse |
        Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    $shipResponse = Join-Path $Output ($shipSpec[0] + '.rsp')
    [IO.File]::WriteAllLines($shipResponse, $shipLines, [Text.UTF8Encoding]::new($false))
    & 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@' + $shipResponse) 2>&1 |
        Tee-Object -FilePath (Join-Path $Output ($shipSpec[0] + '-compile.log'))
    if ($LASTEXITCODE -ne 0) { throw ($shipSpec[0] + ' integration compilation failed.') }
    Write-Output ($shipSpec[0] + ' integration compilation passed.')
}
