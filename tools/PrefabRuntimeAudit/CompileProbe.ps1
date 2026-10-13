param(
    [Parameter(Mandatory=$true)][string]$BaselineExecutable,
    [switch]$ValidationOnly,
    [ValidatePattern('^v[0-9]+$')][string]$Revision = 'v2'
)
$ErrorActionPreference = 'Stop'
$root = 'D:\Game\Artifacts\PrefabRuntimeOptimization20261014'
$exe = [IO.Path]::GetFullPath($BaselineExecutable)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Baseline Player executable required.' }
if (-not $ValidationOnly -and -not $exe.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Canonical probe must compile against this audit baseline.' }
$target = Join-Path $root $(if($ValidationOnly){'HarnessValidation\StaticCompile-'+$Revision}else{'ProbeTools\MeasuredProbe-'+$Revision})
if (Test-Path -LiteralPath $target) { throw 'Preserve existing compilation evidence.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
$source = Join-Path $PSScriptRoot 'PrefabRuntimePlayerProbe.cs'
$savedSource = Join-Path $target 'PrefabRuntimePlayerProbe.cs'
Copy-Item -LiteralPath $source -Destination $savedSource
$probe = 'ShatteredSail.PrefabRuntimeAudit.dll'
$managed = Join-Path ([IO.Path]::GetDirectoryName($exe)) ([IO.Path]::GetFileNameWithoutExtension($exe)+'_Data\Managed')
$refs = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File | Where-Object { $_.Name -notin @($probe,'ShatteredSail.RuntimeMemoryAudit.dll') } | Sort-Object Name)
$arguments = @('-target:library','-nostdlib+','-langversion:9.0','-deterministic+','-optimize+','-debug:portable','-nologo','-define:UNITY_6000_3,UNITY_STANDALONE_WIN,UNITY_STANDALONE')
$arguments += @($refs | ForEach-Object { '-r:"' + $_.FullName + '"' })
$arguments += '-out:"' + (Join-Path $target $probe) + '"'
$arguments += '"' + $savedSource + '"'
$rsp = Join-Path $target 'Probe.rsp'
$arguments | Set-Content -LiteralPath $rsp -Encoding utf8
& 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\NetCoreRuntime\dotnet.exe' 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\DotNetSdkRoslyn\csc.dll' ('@' + $rsp) *> (Join-Path $target 'compile.log')
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $target 'compile.log'); throw 'Probe compilation failed; preserve log.' }
[pscustomobject]@{
    AuditIdentity='PrefabRuntimeOptimization20261014';Revision=$Revision;ValidationOnly=[bool]$ValidationOnly;BaselineExecutable=$exe
    SourceSHA256=(Get-FileHash -LiteralPath $source).Hash;ProbeSHA256=(Get-FileHash -LiteralPath (Join-Path $target $probe)).Hash
    ReferencedAssemblies=@($refs | ForEach-Object { [pscustomobject]@{Name=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $target 'compile-parity.json') -Encoding utf8
Write-Output ('PROBE_COMPILE_PASS='+$target)
