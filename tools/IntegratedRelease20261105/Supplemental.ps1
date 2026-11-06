param(
    [Parameter(Mandatory=$true)][ValidateSet('RamInstalled','Visual')][string]$Stage,
    [ValidatePattern('^[0-9]{2}$')][string]$Revision = '03'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$runRoot = Join-Path $workspace 'Artifacts/IntegratedRelease20261105'
$project = Join-Path $workspace 'tmp/shipyard-detail-clean-20261011'
$manifest = Join-Path $runRoot ('Snapshot' + $Revision + '/source-manifest.csv')
$runner = Join-Path $workspace 'tools/ReleaseBuild/RunTests.ps1'
$lease = Join-Path $project 'IntegratedRelease20261105.lock'
if (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease).Trim() -ne $runRoot) { throw 'Task lease is missing.' }
$saved = @{}
$variables = @('ART_OPT_RAM_COMPRESSION','ART_OPT_RAM_QUALIFIED_REPORT','ART_OPT_RAM_REPORT_DIR','COMIC_CAPTURE_4K')
foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    if ($Stage -eq 'RamInstalled') {
        $qualified = Join-Path $workspace 'outputs/comic-pagination-20261103/art-opt-01-20261104/ramall/ram-compression/report.json'
        if ((Get-FileHash -LiteralPath $qualified).Hash -ne '81A97A2B9E6D56AEDD21E6178D9090BD26F7413A7ECA1F01A95B42FEDE061F9B') { throw 'Qualified report changed.' }
        $env:ART_OPT_RAM_COMPRESSION = 'installed'
        $env:ART_OPT_RAM_QUALIFIED_REPORT = $qualified
        $env:ART_OPT_RAM_REPORT_DIR = Join-Path $workspace ('outputs/integrated-release-20261105/ram-installed-' + $Revision)
        & $runner -Platform EditMode -ProjectPath $project -Output (Join-Path $runRoot ('RamInstalled' + $Revision)) -SourceManifest $manifest -TestFilter 'ArtOptimization10_InstalledResourcesKeepQualifiedPixelsAndNativeMemory' -TimeoutMinutes 35
    } else {
        $env:ART_OPT_RAM_COMPRESSION = $null
        $env:COMIC_CAPTURE_4K = '1'
        $filter = 'ArtOptimization02_;ArtOptimization03_;ArtOptimization04_;ArtOptimization05_;ArtOptimization06_;ArtOptimization07_;ArtOptimization08_;ArtOptimization09_;AdditionalVfx_;AdditionalPortVfx_;AdditionalBattleVfx_;RamComic_PlayerPortSide_;RamComic_PlayerStarboardSide_;RamComic_BossPortSide_;RamComic_BossStarboardSide_;SettlementReturnE2E_'
        & $runner -Platform PlayMode -ProjectPath $project -Output (Join-Path $runRoot ('Visual' + $Revision)) -SourceManifest $manifest -TestFilter $filter -CaptureVisuals -TimeoutMinutes 60
    }
    $testExit = $LASTEXITCODE
} finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
}
exit $testExit
