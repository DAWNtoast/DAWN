param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputTag = 'before',
    [int]$Seeds = 16,
    [int]$MaxTurns = 12,
    [ValidateSet('base','mature')][string]$Cohort = 'base',
    [switch]$CaptureOnly,
    [switch]$UseSnapshot
)
$ErrorActionPreference = 'Stop'
$probeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$probeRoot = Join-Path $probeWorkspace ('outputs/naval-balance-20261024/' + $OutputTag)
$probeSnapshot = Join-Path $probeRoot 'CoreSnapshot'
$probeUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $probeRoot,$probeSnapshot | Out-Null
if (!$UseSnapshot) {
    if (Test-Path (Join-Path $probeRoot 'source-hashes.csv')) { throw 'An existing source snapshot is immutable. Use -UseSnapshot or choose a new OutputTag.' }
    $probeHashes = foreach ($probeFile in (Get-ChildItem -LiteralPath (Join-Path $probeWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)) {
        $probeHash = (Get-FileHash -LiteralPath $probeFile.FullName -Algorithm SHA256).Hash
        $probeCopy = Join-Path $probeSnapshot $probeFile.Name
        Copy-Item -LiteralPath $probeFile.FullName -Destination $probeCopy
        if ((Get-FileHash -LiteralPath $probeFile.FullName).Hash -ne $probeHash -or (Get-FileHash -LiteralPath $probeCopy).Hash -ne $probeHash) { throw 'Core changed during snapshot capture.' }
        [pscustomobject]@{Source=$probeFile.FullName;Snapshot=$probeCopy;SHA256=$probeHash}
    }
    $probeHashes | Export-Csv (Join-Path $probeRoot 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $probeWorkspace 'outputs/naval-balance-20261024/calibration-loadouts.csv') -Destination (Join-Path $probeRoot 'calibration-loadouts.csv')
    Write-Output "Captured $($probeHashes.Count) production Core files. This baseline snapshot is now immutable."
}
$probeHashes = @(Import-Csv (Join-Path $probeRoot 'source-hashes.csv'))
foreach ($probeHash in $probeHashes) {
    if ((Get-FileHash -LiteralPath $probeHash.Snapshot).Hash -ne $probeHash.SHA256) { throw 'Frozen snapshot changed.' }
}
if ($CaptureOnly) { return }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NavalBalanceProbe.cs') -Destination (Join-Path $probeRoot 'NavalBalanceProbe.cs')
$probeSources = @($probeHashes | ForEach-Object {
    if ($OutputTag -eq 'before') {
        $authoritative = Join-Path $probeWorkspace ('outputs/naval-balance-20261024/Inputs/CoreSnapshot/' + (Split-Path $_.Snapshot -Leaf))
        if ((Get-FileHash -LiteralPath $authoritative).Hash -ne $_.SHA256) { throw 'Authoritative before snapshot differs.' }
        $authoritative
    } else { $_.Snapshot }
}) + (Join-Path $probeRoot 'NavalBalanceProbe.cs')
$probeExe = Join-Path $probeRoot 'NavalBalanceProbe.exe'
$probeArgs = @("$probeUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$probeExe")
foreach ($probeAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $probeArgs += "-r:$probeUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$probeAssembly" }
$probeArgs += $probeSources
& "$probeUnity/NetCoreRuntime/dotnet.exe" @probeArgs 2>&1 | Tee-Object -FilePath (Join-Path $probeRoot 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval probe compile failed.' }
$probeResultRoot = $probeRoot
if ($Cohort -eq 'mature') {
    $probeResultRoot = Join-Path $probeRoot 'formed'
    New-Item -ItemType Directory -Force -Path $probeResultRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $probeWorkspace 'outputs/naval-balance-20261024/mature-loadouts.csv') -Destination (Join-Path $probeResultRoot 'calibration-loadouts.csv')
}
& "$probeUnity/MonoBleedingEdge/bin/mono.exe" $probeExe $probeResultRoot $Seeds $MaxTurns $Cohort 2>&1 | Tee-Object -FilePath (Join-Path $probeResultRoot 'run.log')
if ($LASTEXITCODE -ne 0) { throw 'Naval probe run failed.' }
foreach ($probeHash in $probeHashes) {
    if ((Get-FileHash -LiteralPath $probeHash.Snapshot).Hash -ne $probeHash.SHA256) { throw 'Frozen snapshot changed during run.' }
}
Get-FileHash -LiteralPath (Join-Path $probeRoot 'NavalBalanceProbe.cs'),(Join-Path $probeResultRoot 'calibration-loadouts.csv') | Export-Csv (Join-Path $probeResultRoot 'probe-input-hashes.csv') -NoTypeInformation -Encoding utf8
Write-Output 'PASS: production snapshot unchanged. Result frequencies describe these finite policies, not global school win rates.'
