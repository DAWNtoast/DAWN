param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$BuildName,
    [ValidatePattern('^v[0-9]+$')][string]$Revision = 'v2'
)
$ErrorActionPreference = 'Stop'
$auditRoot = 'D:\Game\Artifacts\PrefabRuntimeOptimization20261014'
$buildRoot = [IO.Path]::GetFullPath((Join-Path $auditRoot $BuildName))
$windowsRoot = Join-Path $buildRoot 'Windows'
$canonicalRoot = Join-Path $auditRoot ('ProbeTools\MeasuredProbe-'+$Revision)
$probeName = 'ShatteredSail.PrefabRuntimeAudit.dll'
$probeRelative = 'PrefabAudit_Data\Managed\' + $probeName
if(-not $buildRoot.StartsWith($auditRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'This round diagnostic build required.'}
if(@(Get-Process -Name Unity,MemoryAudit,PrefabAudit -ErrorAction SilentlyContinue).Count -gt 0){throw 'Do not alter a running build or Player.'}
if(-not(Test-Path -LiteralPath (Join-Path $windowsRoot 'PrefabAudit.exe') -PathType Leaf)){throw 'Complete diagnostic build required.'}
$compile=Get-Content -LiteralPath (Join-Path $canonicalRoot 'compile-parity.json') -Raw|ConvertFrom-Json
$expectedProbeHash = $compile.ProbeSHA256
if ($compile.AuditIdentity -ne 'PrefabRuntimeOptimization20261014' -or $compile.Revision -ne $Revision -or $compile.ValidationOnly -or $expectedProbeHash -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Wrong canonical probe identity/hash manifest.' }
if((Get-FileHash -LiteralPath (Join-Path $canonicalRoot $probeName)).Hash-ne$expectedProbeHash -or $compile.ProbeSHA256-ne$expectedProbeHash){throw 'Canonical measured probe hash changed.'}
if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'PrefabRuntimePlayerProbe.cs')).Hash-ne$compile.SourceSHA256){throw 'Probe source differs from compiled canonical DLL.'}
$evidenceRoot=Join-Path $buildRoot 'DiagnosticProbePostprocess'
if(Test-Path -LiteralPath $evidenceRoot){throw 'Preserve earlier evidence; postprocess only once per fresh build.'}
$initializers=Get-Content -LiteralPath (Join-Path $windowsRoot 'PrefabAudit_Data\RuntimeInitializeOnLoads.json') -Raw|ConvertFrom-Json
$bootstrap=@($initializers.root|Where-Object{$_.assemblyName-eq'ShatteredSail.PrefabRuntimeAudit' -and $_.nameSpace-eq'ShatteredSail.PrefabAudit' -and $_.className-eq'PrefabRuntimePlayerProbe' -and $_.methodName-eq'Bootstrap'})
if($bootstrap.Count-ne1){throw 'Expected diagnostic Bootstrap initializer was not preserved.'}
New-Item -ItemType Directory -Path $evidenceRoot|Out-Null
$before=@(Get-ChildItem -LiteralPath $windowsRoot -Recurse -File|Sort-Object FullName|ForEach-Object{[pscustomobject]@{Path=$_.FullName.Substring($windowsRoot.Length+1);Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$before|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $evidenceRoot 'build-original-manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $windowsRoot $probeRelative) -Destination (Join-Path $evidenceRoot ('Original.'+$probeName))
Copy-Item -LiteralPath (Join-Path $canonicalRoot $probeName) -Destination (Join-Path $windowsRoot $probeRelative) -Force
$parity=@(foreach($file in $before){$hash=(Get-FileHash -LiteralPath (Join-Path $windowsRoot $file.Path)).Hash;[pscustomobject]@{Path=$file.Path;Before=$file.SHA256;After=$hash;Changed=$file.SHA256-ne$hash}})
$parity|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $evidenceRoot 'production-file-parity.json') -Encoding utf8
$changed=@($parity|Where-Object Changed)
if(@(Get-ChildItem -LiteralPath $windowsRoot -Recurse -File).Count-ne$before.Count -or @($changed|Where-Object Path -ne $probeRelative).Count-gt0){throw 'Unexpected production build changes; preserve and inspect evidence.'}
[pscustomobject]@{Result='PASS';Scope='Diagnostic postprocessing only. Production assemblies, resources and executable remain byte-identical. Only optional probe DLL replaced; never apply to formal release.';ProbeSHA256=$expectedProbeHash;SourceSHA256=$compile.SourceSHA256;FilesVerified=$before.Count;Changed=$changed;RuntimeInitializer=$bootstrap;CompletedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $evidenceRoot 'probe-update.json') -Encoding utf8
Write-Output ('CANONICAL_PROBE_APPLIED='+$expectedProbeHash)
