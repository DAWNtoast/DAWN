param(
    [string]$Output = 'D:/Game/docs/BoardingBossRewards20261015.csv',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$rewardsWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$rewardsBuild = Join-Path $rewardsWorkspace 'Artifacts/BoardingRewards20261015/Build'
$rewardsSnapshot = Join-Path $rewardsWorkspace 'Artifacts/BoardingRewards20261015/CoreSnapshot'
New-Item -ItemType Directory -Force -Path $rewardsBuild,$rewardsSnapshot | Out-Null
$rewardsSources = @(Get-ChildItem -LiteralPath (Join-Path $rewardsWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$rewardsSources += Get-Item -LiteralPath (Join-Path $rewardsWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$rewardsHashes = foreach ($rewardsSource in $rewardsSources) {
    $rewardsCopy = Join-Path $rewardsSnapshot $rewardsSource.Name
    Copy-Item -LiteralPath $rewardsSource.FullName -Destination $rewardsCopy
    [pscustomobject]@{File=$rewardsSource.Name;SHA256=(Get-FileHash -LiteralPath $rewardsCopy -Algorithm SHA256).Hash}
}
$rewardsHashes | Export-Csv -LiteralPath (Join-Path $rewardsBuild '../source-hashes.csv') -NoTypeInformation -Encoding utf8
$rewardsFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$rewardsExe = Join-Path $rewardsBuild 'ExportRewards.exe'
$rewardsArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$rewardsExe")
foreach ($rewardsAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $rewardsArgs += '-r:' + (Join-Path $rewardsFramework $rewardsAssembly) }
$rewardsArgs += @(Get-ChildItem -LiteralPath $rewardsSnapshot -Filter '*.cs' | ForEach-Object FullName)
$rewardsArgs += Join-Path $PSScriptRoot 'ExportRewards.cs'
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @rewardsArgs
if ($LASTEXITCODE -ne 0) { throw 'Reward table compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $rewardsExe $Output
if ($LASTEXITCODE -ne 0) { throw 'Reward table export failed.' }
