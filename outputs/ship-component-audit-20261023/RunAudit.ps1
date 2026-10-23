$ErrorActionPreference = 'Stop'
$auditRoot = $PSScriptRoot
$auditProject = 'D:/Game'
$auditUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditSnapshot = Join-Path $auditRoot 'CoreSnapshot'
New-Item -ItemType Directory -Force -Path $auditSnapshot | Out-Null
$auditSources = @(Get-ChildItem -LiteralPath (Join-Path $auditProject 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$auditHashes = foreach ($auditSource in $auditSources) {
    $auditDestination = Join-Path $auditSnapshot $auditSource.Name
    $auditOriginal = (Get-FileHash -LiteralPath $auditSource.FullName -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $auditSource.FullName -Destination $auditDestination
    $auditCopied = (Get-FileHash -LiteralPath $auditDestination -Algorithm SHA256).Hash
    if ($auditOriginal -ne $auditCopied) { throw 'Core snapshot hash mismatch' }
    [pscustomobject]@{ Source=$auditSource.FullName; Snapshot=$auditDestination; SHA256=$auditOriginal }
}
$auditHashes | Export-Csv -LiteralPath (Join-Path $auditRoot 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$auditCompiler = Join-Path $auditUnity 'DotNetSdkRoslyn/csc.dll'
$auditRuntime = Join-Path $auditUnity 'NetCoreRuntime/dotnet.exe'
$auditMono = Join-Path $auditUnity 'MonoBleedingEdge/bin/mono.exe'
$auditFramework = Join-Path $auditUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$auditRefs = @('mscorlib.dll','System.dll','System.Core.dll') | ForEach-Object { '-r:' + (Join-Path $auditFramework $_) }
$auditDll = Join-Path $auditRoot 'ShatteredSail.ComponentAudit.Core.dll'
$auditCompile = @($auditCompiler,'-nologo','-target:library','-langversion:9','-noconfig','-nostdlib',"-out:$auditDll") + $auditRefs + @($auditHashes | ForEach-Object Snapshot)
& $auditRuntime @auditCompile 2>&1 | Tee-Object -FilePath (Join-Path $auditRoot 'core-compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Core compile failed' }
$auditExe = Join-Path $auditRoot 'ComponentCombatAudit.exe'
$auditCompile = @($auditCompiler,'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$auditExe","-r:$auditDll") + $auditRefs + (Join-Path $auditRoot 'ComponentCombatAudit.cs')
& $auditRuntime @auditCompile 2>&1 | Tee-Object -FilePath (Join-Path $auditRoot 'audit-compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Audit compile failed' }
$auditCatalog = Join-Path $auditProject 'UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv'
Get-FileHash -LiteralPath $auditCatalog -Algorithm SHA256 | Format-List | Out-File (Join-Path $auditRoot 'catalog-hash.txt')
& $auditMono $auditExe $auditCatalog $auditRoot 2>&1 | Tee-Object -FilePath (Join-Path $auditRoot 'run.log')
if ($LASTEXITCODE -ne 0) { throw 'Audit execution failed' }
