param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$BuildName)
$ErrorActionPreference='Stop'
$memoryRoot='D:\Game\Artifacts\MemoryRelease20261015'
$memoryBuild=[IO.Path]::GetFullPath((Join-Path $memoryRoot $BuildName))
if($BuildName -notlike 'Optimized*' -or -not $memoryBuild.StartsWith($memoryRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Optimized diagnostic builds only; never formal release.'}
if(@(Get-Process -Name Unity,MemoryAudit -ErrorAction SilentlyContinue).Count){throw 'Do not alter running build or Player.'}
$memoryWindows=Join-Path $memoryBuild 'Windows'
$memoryCanonical=Join-Path $memoryRoot 'CanonicalProbe'
$memoryProbe='ShatteredSail.RuntimeMemoryAudit.dll'
$memoryRelative='MemoryAudit_Data\Managed\'+$memoryProbe
$memoryParity=Get-Content -LiteralPath (Join-Path $memoryCanonical 'probe-parity.json') -Raw|ConvertFrom-Json
if((Get-FileHash -LiteralPath (Join-Path $memoryCanonical $memoryProbe)).Hash -ne $memoryParity.ProbeSHA256 -or (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'RuntimeMemoryPlayerProbe.cs')).Hash -ne $memoryParity.SourceSHA256){throw 'Canonical probe/source mismatch.'}
if(-not(Test-Path -LiteralPath (Join-Path $memoryWindows 'MemoryAudit.exe'))){throw 'Diagnostic build missing.'}
$memoryInitializers=Get-Content -LiteralPath (Join-Path $memoryWindows 'MemoryAudit_Data/RuntimeInitializeOnLoads.json') -Raw|ConvertFrom-Json
$memoryBootstrap=@($memoryInitializers.root|Where-Object {$_.assemblyName -eq 'ShatteredSail.RuntimeMemoryAudit' -and $_.nameSpace -eq 'ShatteredSail.MemoryAudit' -and $_.className -eq 'RuntimeMemoryPlayerProbe' -and $_.methodName -eq 'Bootstrap'})
if($memoryBootstrap.Count -ne 1){throw 'Diagnostic Bootstrap initializer missing.'}
$memoryEvidence=Join-Path $memoryBuild 'DiagnosticProbePostprocess'
if(Test-Path -LiteralPath $memoryEvidence){throw 'Preserve earlier postprocess evidence.'}
New-Item -ItemType Directory -Path $memoryEvidence|Out-Null
$memoryBefore=@(Get-ChildItem -LiteralPath $memoryWindows -Recurse -File|Sort-Object FullName|ForEach-Object {[pscustomobject]@{Path=$_.FullName.Substring($memoryWindows.Length+1);Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$memoryBefore|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'original-manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $memoryWindows $memoryRelative) -Destination (Join-Path $memoryEvidence ('Original.'+$memoryProbe))
Copy-Item -LiteralPath (Join-Path $memoryCanonical $memoryProbe) -Destination (Join-Path $memoryWindows $memoryRelative) -Force
$memoryAfter=@(foreach($memoryFile in $memoryBefore){$memoryHash=(Get-FileHash -LiteralPath (Join-Path $memoryWindows $memoryFile.Path)).Hash;[pscustomobject]@{Path=$memoryFile.Path;Before=$memoryFile.SHA256;After=$memoryHash;Changed=$memoryFile.SHA256 -ne $memoryHash}})
$memoryAfter|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'file-parity.json') -Encoding utf8
$memoryChanged=@($memoryAfter|Where-Object Changed)
if(@(Get-ChildItem -LiteralPath $memoryWindows -Recurse -File).Count -ne $memoryBefore.Count -or @($memoryChanged|Where-Object Path -ne $memoryRelative).Count){throw 'Unexpected production file changes.'}
[pscustomobject]@{Result='PASS';ProbeSHA256=$memoryParity.ProbeSHA256;SourceSHA256=$memoryParity.SourceSHA256;FilesVerified=$memoryBefore.Count;Changed=$memoryChanged;Scope='Only diagnostic probe DLL can change. Production assemblies/resources/executable remain identical. Never apply to final delivery.'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $memoryEvidence 'probe-update.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $memoryEvidence 'probe-update.json')
