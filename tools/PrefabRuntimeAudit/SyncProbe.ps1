param(
    [Parameter(Mandatory=$true)][string]$Project,
    [ValidatePattern('^v[0-9]+$')][string]$Revision = 'v2'
)
$ErrorActionPreference='Stop'
$isolated=[IO.Path]::GetFullPath($Project).TrimEnd('\')
if(-not $isolated.StartsWith('D:\Game\tmp\',[StringComparison]::OrdinalIgnoreCase) -or -not(Test-Path -LiteralPath (Join-Path $isolated '.prefab-runtime-owned.json'))){throw 'Owned isolated prefab audit project required.'}
if(@(Get-Process -Name Unity,PrefabAudit,MemoryAudit -ErrorAction SilentlyContinue).Count){throw 'Coordinate idle Unity/Player before syncing probe source.'}
$source=Join-Path $PSScriptRoot 'PrefabRuntimePlayerProbe.cs'
$target=Join-Path $isolated 'Assets\PrefabRuntimeAudit\PrefabRuntimePlayerProbe.cs'
if(-not(Test-Path -LiteralPath $target) -or -not(Test-Path -LiteralPath (Join-Path $isolated 'Assets\PrefabRuntimeAudit\ShatteredSail.PrefabRuntimeAudit.asmdef'))){throw 'Use PrepareProbe for initial setup.'}
if(-not([IO.File]::ReadAllText($source).Contains('probeRevision = "'+$Revision+'"'))){throw 'Source revision differs from requested revision.'}
$archive=Join-Path 'D:\Game\Artifacts\PrefabRuntimeOptimization20261014\ProbeTools' ('SourceSync-'+$Revision)
if(Test-Path -LiteralPath $archive){throw 'Preserve earlier source sync evidence.'}
New-Item -ItemType Directory -Path $archive|Out-Null
Copy-Item -LiteralPath $target -Destination (Join-Path $archive 'Before.PrefabRuntimePlayerProbe.cs')
$before=(Get-FileHash -LiteralPath $target).Hash
Copy-Item -LiteralPath $source -Destination $target -Force
$after=(Get-FileHash -LiteralPath $target).Hash
if($after-ne(Get-FileHash -LiteralPath $source).Hash){throw 'Probe source synchronization mismatch.'}
[pscustomobject]@{Revision=$Revision;Project=$isolated;Whitelist=@('Assets/PrefabRuntimeAudit/PrefabRuntimePlayerProbe.cs');BeforeSHA256=$before;AfterSHA256=$after;CompletedUtc=[DateTime]::UtcNow.ToString('o')}|
    ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $archive 'source-sync.json') -Encoding utf8
Write-Output ('PROBE_SOURCE_SYNCED='+$after)
