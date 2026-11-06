param([string]$CoreSource='', [string]$Output='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$CoreSource){$CoreSource=Join-Path $taskRoot 'UnityClient/Assets/_Project/Core/Runtime'}
if(!$Output){$Output=Join-Path $taskRoot 'Artifacts/BoardingFieldBalance20261024/FieldRegression'}
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$taskNunit=Get-ChildItem -LiteralPath (Join-Path $taskRoot 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object {Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
Copy-Item -LiteralPath $taskNunit -Destination (Join-Path $Output 'nunit.framework.dll') -Force
$taskFiles=@(Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs' | Sort-Object Name)
$taskFiles+=Get-Item -LiteralPath (Join-Path $taskRoot 'UnityClient/Assets/_Project/Tests/EditMode/CoreBoardingFieldBalance20261024Tests.cs')
$taskFiles+=Get-Item -LiteralPath (Join-Path $taskRoot 'tools/CourseTargetLock20261014/CoreRunner.cs')
$taskHashes=@($taskFiles | ForEach-Object {[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$taskHashes | Export-Csv -LiteralPath (Join-Path $Output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$taskExe=Join-Path $Output 'FieldRegression.exe'
$taskArgs=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe","-r:$taskNunit")
foreach($taskAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')){$taskArgs+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$taskAssembly"}
$taskArgs+=@($taskFiles.FullName)
& "$taskUnity/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Field regression compilation failed'}
& "$taskUnity/MonoBleedingEdge/bin/mono.exe" $taskExe $Output 2>&1 | Tee-Object -FilePath (Join-Path $Output 'tests.log')
if($LASTEXITCODE -ne 0){throw 'Field regression failed'}
foreach($taskHash in $taskHashes){if((Get-FileHash -LiteralPath $taskHash.Source).Hash -ne $taskHash.SHA256){throw 'Core snapshot changed during regression'}}
