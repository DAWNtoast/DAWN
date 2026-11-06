$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditRoot = Join-Path $auditWorkspace 'outputs/naval-balance-20261024'
New-Item -ItemType Directory -Force -Path $auditRoot | Out-Null
$auditUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditSources = @((Get-ChildItem -LiteralPath (Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs').FullName)
$auditSources += Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs'
$auditSources += Join-Path $PSScriptRoot 'BossProfileAudit.cs'
$auditHashes = @($auditSources | ForEach-Object { [pscustomobject]@{ Source = $_; SHA256 = (Get-FileHash -LiteralPath $_).Hash } })
$auditHashes | Export-Csv -LiteralPath (Join-Path $auditRoot 'boss-source-hashes.csv') -NoTypeInformation -Encoding utf8
$auditArgs = @("$auditUnity/DotNetSdkRoslyn/csc.dll", '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$auditRoot/BossProfileAudit.exe")
foreach ($auditAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $auditArgs += "-r:$auditUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$auditAssembly"
}
$auditArgs += $auditSources
& "$auditUnity/NetCoreRuntime/dotnet.exe" @auditArgs
if ($LASTEXITCODE -ne 0) { throw 'Boss audit compile failed' }
& "$auditUnity/MonoBleedingEdge/bin/mono.exe" "$auditRoot/BossProfileAudit.exe" $auditRoot
if ($LASTEXITCODE -ne 0) { throw 'Boss audit run failed' }
foreach ($auditHash in $auditHashes) {
    if ((Get-FileHash -LiteralPath $auditHash.Source).Hash -ne $auditHash.SHA256) { throw ('Source changed during audit: ' + $auditHash.Source) }
}
Write-Output 'PASS source hashes stable during boss audit.'
