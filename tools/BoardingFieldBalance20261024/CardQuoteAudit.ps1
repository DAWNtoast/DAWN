param([string]$CoreSource='D:/Game/outputs/boarding-field-balance-20261024/experiments/card-a-core',[string]$Output='D:/Game/outputs/boarding-field-balance-20261024/card-a-quote-audit')
$ErrorActionPreference='Stop'
$cardUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$cardExe=Join-Path $Output 'CardQuoteAudit.exe'
$cardArgs=@("$cardUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:CardQuoteAudit','-langversion:9','-noconfig','-nostdlib',"-out:$cardExe")
foreach($cardAssembly in @('mscorlib.dll','System.dll','System.Core.dll')){$cardArgs+="-r:$cardUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$cardAssembly"}
$cardArgs+=@((Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs').FullName)
$cardArgs+=Join-Path $PSScriptRoot 'CardQuoteAudit.cs'
& "$cardUnity/NetCoreRuntime/dotnet.exe" @cardArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Card quote audit compile failed'}
& "$cardUnity/MonoBleedingEdge/bin/mono.exe" $cardExe $Output 2>&1 | Tee-Object -FilePath (Join-Path $Output 'audit.log')
if($LASTEXITCODE -ne 0){throw 'Card quote audit failed'}
