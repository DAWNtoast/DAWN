param(
    [string]$RunTag = 'Current',
    [string[]]$Fixtures = @('CoreThunderCatalogRevisionTests', 'CoreTideCatalogRevisionTests')
)
$ErrorActionPreference = 'Stop'
$thunderWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$thunderOutput = Join-Path $thunderWorkspace ('Artifacts/ThunderRules20261024/' + $RunTag)
$thunderBuild = Join-Path $thunderOutput 'Build'
$thunderUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$thunderFramework = Join-Path $thunderUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$thunderNunit = Get-ChildItem -LiteralPath (Join-Path $thunderWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$thunderTests = Join-Path $thunderWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$thunderFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $thunderTests ($_ + '.cs')) })
$thunderSources = @(Get-ChildItem -LiteralPath (Join-Path $thunderWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$thunderSources += $thunderFixtures
$thunderSources += Get-Item -LiteralPath (Join-Path $thunderWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$thunderSources += Get-Item -LiteralPath (Join-Path $thunderWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
New-Item -ItemType Directory -Force -Path $thunderBuild | Out-Null
$thunderHashes = @($thunderSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$thunderHashes | Export-Csv -LiteralPath (Join-Path $thunderOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $thunderNunit -Destination (Join-Path $thunderBuild 'nunit.framework.dll') -Force
$thunderFixtures.Name | Set-Content -LiteralPath (Join-Path $thunderOutput 'fixtures.txt') -Encoding utf8
$thunderExe = Join-Path $thunderBuild 'ThunderRulesTests.exe'
$thunderArgs = @((Join-Path $thunderUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$thunderExe", "-r:$thunderNunit")
foreach ($thunderAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $thunderArgs += '-r:' + (Join-Path $thunderFramework $thunderAssembly)
}
$thunderArgs += $thunderSources.FullName
& (Join-Path $thunderUnity 'NetCoreRuntime/dotnet.exe') @thunderArgs 2>&1 | Tee-Object -FilePath (Join-Path $thunderOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Thunder rules Core compilation failed.' }
& (Join-Path $thunderUnity 'MonoBleedingEdge/bin/mono.exe') $thunderExe $thunderOutput 2>&1 | Tee-Object -FilePath (Join-Path $thunderOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Thunder rules Core tests failed; see results.xml and failures.txt.' }
foreach ($thunderHash in $thunderHashes) {
    if ((Get-FileHash -LiteralPath $thunderHash.Source -Algorithm SHA256).Hash -ne $thunderHash.SHA256) {
        throw ('Source changed during tests: ' + $thunderHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'


