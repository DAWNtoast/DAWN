$ErrorActionPreference = 'Stop'
$quantWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$quantRoot = Join-Path $quantWorkspace 'outputs/six-school-audit-20261024/quantitative'
New-Item -ItemType Directory -Force -Path $quantRoot | Out-Null
$quantUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$quantSources = @((Get-ChildItem -LiteralPath (Join-Path $quantWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs').FullName)
$quantSources += Join-Path $quantWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs'
$quantSources += Join-Path $PSScriptRoot 'ThunderBloodQuantitative.cs'
$quantHashes = @($quantSources | ForEach-Object { [pscustomobject]@{ Source = $_; SHA256 = (Get-FileHash -LiteralPath $_).Hash } })
$quantHashes | Export-Csv -LiteralPath (Join-Path $quantRoot 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$quantArgs = @("$quantUnity/DotNetSdkRoslyn/csc.dll", '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$quantRoot/ThunderBloodQuantitative.exe")
foreach ($quantAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $quantArgs += "-r:$quantUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$quantAssembly"
}
$quantArgs += $quantSources
& "$quantUnity/NetCoreRuntime/dotnet.exe" @quantArgs
if ($LASTEXITCODE -ne 0) { throw 'Probe compile failed' }
& "$quantUnity/MonoBleedingEdge/bin/mono.exe" "$quantRoot/ThunderBloodQuantitative.exe" $quantRoot
if ($LASTEXITCODE -ne 0) { throw 'Probe run failed' }
foreach ($quantHash in $quantHashes) {
    if ((Get-FileHash -LiteralPath $quantHash.Source).Hash -ne $quantHash.SHA256) { throw ('Source changed during probe: ' + $quantHash.Source) }
}
Write-Output 'PASS source hashes stable during quantitative probe.'
