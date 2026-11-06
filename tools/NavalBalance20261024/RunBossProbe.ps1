param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputTag = 'before',
    [int]$Seeds = 4,
    [int]$MaxTurns = 12,
    [string]$SourceSnapshot = '',
    [string]$BossFilter = '*',
    [string]$ProfileCandidates = '',
    [string]$SharedProbeSource = '',
    [string]$LoadoutsFile = '',
    [switch]$Mature,
    [switch]$Survival,
    [switch]$UseSnapshot
)
$ErrorActionPreference = 'Stop'
$bossWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bossRoot = Join-Path $bossWorkspace ('outputs/naval-balance-20261024/boss-' + $OutputTag)
$bossSnapshot = Join-Path $bossRoot 'CoreSnapshot'
$bossUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
if ($Seeds -lt 1 -or $MaxTurns -lt 1) { throw 'Seeds and MaxTurns must be positive.' }
if (!$SourceSnapshot) { $SourceSnapshot = Join-Path $bossWorkspace 'outputs/naval-balance-20261024/Inputs/CoreSnapshot' }
$bossSource = [IO.Path]::GetFullPath($SourceSnapshot)
New-Item -ItemType Directory -Force -Path $bossRoot,$bossSnapshot | Out-Null
if (!$UseSnapshot) {
    if (Test-Path -LiteralPath (Join-Path $bossRoot 'source-hashes.csv')) { throw 'Snapshot already frozen. Use -UseSnapshot or another OutputTag.' }
    $bossHashes = foreach ($bossFile in (Get-ChildItem -LiteralPath $bossSource -Filter '*.cs' | Sort-Object Name)) {
        $bossHash = (Get-FileHash -LiteralPath $bossFile.FullName).Hash
        $bossCopy = Join-Path $bossSnapshot $bossFile.Name
        Copy-Item -LiteralPath $bossFile.FullName -Destination $bossCopy
        if ((Get-FileHash -LiteralPath $bossCopy).Hash -ne $bossHash -or (Get-FileHash -LiteralPath $bossFile.FullName).Hash -ne $bossHash) { throw 'Source changed during snapshot.' }
        [pscustomobject]@{ Source=$bossFile.FullName; Snapshot=$bossCopy; SHA256=$bossHash }
    }
    if (@($bossHashes).Count -lt 20) { throw 'CoreSnapshot is incomplete.' }
    $bossHashes | Export-Csv -LiteralPath (Join-Path $bossRoot 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    if (!$LoadoutsFile) { $LoadoutsFile = Join-Path $bossWorkspace ('outputs/naval-balance-20261024/' + $(if ($Mature) {'mature-loadouts.csv'} else {'calibration-loadouts.csv'})) }
    Copy-Item -LiteralPath $LoadoutsFile -Destination (Join-Path $bossRoot 'calibration-loadouts.csv')
}
$bossHashes = @(Import-Csv -LiteralPath (Join-Path $bossRoot 'source-hashes.csv'))
foreach ($bossHash in $bossHashes) {
    if ((Get-FileHash -LiteralPath $bossHash.Snapshot).Hash -ne $bossHash.SHA256) { throw 'Frozen CoreSnapshot changed.' }
}
if ($ProfileCandidates) { Copy-Item -LiteralPath $ProfileCandidates -Destination (Join-Path $bossRoot 'boss-candidates.csv') }
$bossHelpers = @('BossBalanceProbe.cs','BossProfileAudit.cs','NavalBalanceProbe.cs')
foreach ($bossHelper in $bossHelpers) {
    $bossHelperSource = if ($bossHelper -eq 'NavalBalanceProbe.cs' -and $SharedProbeSource) { $SharedProbeSource } else { Join-Path $PSScriptRoot $bossHelper }
    Copy-Item -LiteralPath $bossHelperSource -Destination (Join-Path $bossRoot $bossHelper)
}
$bossSources = @($bossHashes | ForEach-Object Snapshot) + @($bossHelpers | ForEach-Object { Join-Path $bossRoot $_ })
$bossExe = Join-Path $bossRoot 'BossBalanceProbe.exe'
$bossArgs = @("$bossUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:BossBalanceProbe','-langversion:9','-noconfig','-nostdlib',"-out:$bossExe")
foreach ($bossAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $bossArgs += "-r:$bossUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$bossAssembly" }
$bossArgs += $bossSources
& "$bossUnity/NetCoreRuntime/dotnet.exe" @bossArgs 2>&1 | Tee-Object -FilePath (Join-Path $bossRoot 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boss probe compile failed.' }
$bossCohort = if ($Mature) { 'mature' } else { 'base' }
$bossPolicy = if ($Survival) { 'survival' } else { 'damage' }
& "$bossUnity/MonoBleedingEdge/bin/mono.exe" $bossExe $bossRoot $Seeds $MaxTurns $BossFilter $bossCohort $bossPolicy 2>&1 | Tee-Object -FilePath (Join-Path $bossRoot 'run.log')
if ($LASTEXITCODE -ne 0) { throw 'Boss probe run failed.' }
$bossBefore = @(Import-Csv -LiteralPath (Join-Path $bossWorkspace 'outputs/naval-balance-20261024/Inputs/boss-profiles.csv'))
$bossAfter = @(Import-Csv -LiteralPath (Join-Path $bossRoot 'boss-profiles.csv'))
if ($bossBefore.Count -ne $bossAfter.Count) { throw 'Boss profile set changed; inspect regression baseline.' }
foreach ($bossOld in $bossBefore) {
    $bossNew = @($bossAfter | Where-Object id -eq $bossOld.id)
    if ($bossNew.Count -ne 1) { throw ('Missing/duplicate boss: ' + $bossOld.id) }
    foreach ($bossField in @('boardingHealth','melee','boardingGuard','boardingHealthMultiplier','bowQuality','hullQuality','ramChance')) {
        if ($bossOld.$bossField -ne $bossNew[0].$bossField) { throw ('Protected boarding/contact parameter changed: ' + $bossOld.id + '/' + $bossField) }
    }
}
foreach ($bossHash in $bossHashes) {
    if ((Get-FileHash -LiteralPath $bossHash.Snapshot).Hash -ne $bossHash.SHA256) { throw 'Frozen snapshot changed during run.' }
}
Get-FileHash -LiteralPath @($bossHelpers | ForEach-Object { Join-Path $bossRoot $_ }) | Export-Csv -LiteralPath (Join-Path $bossRoot 'probe-hashes.csv') -NoTypeInformation -Encoding utf8
Get-FileHash -LiteralPath (Join-Path $bossRoot 'calibration-loadouts.csv') | Export-Csv -LiteralPath (Join-Path $bossRoot 'loadout-hash.csv') -NoTypeInformation -Encoding utf8
Write-Output 'PASS: protected boarding/contact parameters unchanged; finite policy samples are not global win rates.'
