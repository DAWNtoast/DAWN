param(
    [string[]]$Fixtures = @('CorePortUnlockTests', 'CoreVoyageDestinationTests', 'CorePortArrivalTests',
        'CoreSeaChartProgressTests', 'CoreGuidedTutorialVoyageTests', 'CoreGuidedBossScriptTests')
)
$ErrorActionPreference = 'Stop'
$portValidationWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$portValidationOutput = [IO.Path]::GetFullPath((Join-Path $portValidationWorkspace 'outputs/port-unlock-progression-20261028/core-tests'))
$portValidationBuild = Join-Path $portValidationOutput 'Build'
$portValidationUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$portValidationFramework = Join-Path $portValidationUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$portValidationNunit = Get-ChildItem -LiteralPath (Join-Path $portValidationWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' |
    ForEach-Object { Join-Path $_.FullName 'net40/unity-custom/nunit.framework.dll' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
$portValidationTests = Join-Path $portValidationWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$portValidationFixtures = @($Fixtures | ForEach-Object { Get-Item -LiteralPath (Join-Path $portValidationTests ($_ + '.cs')) })
$portValidationSources = @(Get-ChildItem -LiteralPath (Join-Path $portValidationWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$portValidationSources += $portValidationFixtures
$portValidationSources += Get-Item -LiteralPath (Join-Path $portValidationWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$portValidationSources += Get-Item -LiteralPath (Join-Path $portValidationWorkspace 'tools/CourseTargetLock20261014/CoreRunner.cs')
function Assert-PortValidationBuildDirectory {
    $portValidationExpected = [IO.Path]::GetFullPath((Join-Path $portValidationWorkspace 'outputs/port-unlock-progression-20261028/core-tests/Build'))
    if ([IO.Path]::GetFullPath($portValidationBuild) -ne $portValidationExpected) { throw 'Unexpected sea-portValidation test build directory.' }
    foreach ($portValidationDirectory in @($portValidationWorkspace, (Join-Path $portValidationWorkspace 'outputs'),
        (Join-Path $portValidationWorkspace 'outputs/port-unlock-progression-20261028'), $portValidationOutput, $portValidationBuild)) {
        if ((Test-Path -LiteralPath $portValidationDirectory) -and
            ((Get-Item -LiteralPath $portValidationDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked test directory: ' + $portValidationDirectory)
        }
    }
}
Assert-PortValidationBuildDirectory
try {
New-Item -ItemType Directory -Force -Path $portValidationBuild | Out-Null
$portValidationHashes = @($portValidationSources | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$portValidationHashes | Export-Csv -LiteralPath (Join-Path $portValidationOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $portValidationNunit -Destination (Join-Path $portValidationBuild 'nunit.framework.dll') -Force
$portValidationFixtures.Name | Set-Content -LiteralPath (Join-Path $portValidationOutput 'fixtures.txt') -Encoding utf8
$portValidationExe = Join-Path $portValidationBuild 'PortUnlockTests.exe'
$portValidationArgs = @((Join-Path $portValidationUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$portValidationExe", "-r:$portValidationNunit")
foreach ($portValidationAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll')) {
    $portValidationArgs += '-r:' + (Join-Path $portValidationFramework $portValidationAssembly)
}
$portValidationArgs += $portValidationSources.FullName
& (Join-Path $portValidationUnity 'NetCoreRuntime/dotnet.exe') @portValidationArgs 2>&1 | Tee-Object -FilePath (Join-Path $portValidationOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Sea-portValidation Core compilation failed.' }
& (Join-Path $portValidationUnity 'MonoBleedingEdge/bin/mono.exe') $portValidationExe $portValidationOutput 2>&1 | Tee-Object -FilePath (Join-Path $portValidationOutput 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Sea-portValidation Core tests failed; see results.xml and failures.txt.' }
foreach ($portValidationHash in $portValidationHashes) {
    if ((Get-FileHash -LiteralPath $portValidationHash.Source -Algorithm SHA256).Hash -ne $portValidationHash.SHA256) {
        throw ('Source changed during tests: ' + $portValidationHash.Source)
    }
}
Write-Output 'PASS source hashes stable during core regression.'

} finally {
    Assert-PortValidationBuildDirectory
    $portValidationConsumers = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('mono.exe', 'dotnet.exe', 'Unity.exe') -and $_.CommandLine -and
        ($_.CommandLine.Contains($portValidationBuild) -or $_.CommandLine.Contains($portValidationBuild.Replace('\', '/')))
    })
    if ($portValidationConsumers.Count) { throw 'Sea-portValidation test binaries are still used by another process.' }
    $portValidationFreed = 0L
    $portValidationRemoved = [Collections.Generic.List[string]]::new()
    foreach ($portValidationName in @('PortUnlockTests.exe', 'nunit.framework.dll')) {
        $portValidationFile = [IO.Path]::GetFullPath((Join-Path $portValidationBuild $portValidationName))
        if ([IO.Path]::GetDirectoryName($portValidationFile) -ne [IO.Path]::GetFullPath($portValidationBuild)) { throw 'Unexpected cleanup target.' }
        if (Test-Path -LiteralPath $portValidationFile) {
            $portValidationItem = Get-Item -LiteralPath $portValidationFile
            if ($portValidationItem.PSIsContainer -or ($portValidationItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw ('Refusing unexpected test artifact: ' + $portValidationFile)
            }
            $portValidationFreed += $portValidationItem.Length
            Remove-Item -LiteralPath $portValidationFile -Force
            if (Test-Path -LiteralPath $portValidationFile) { throw ('Sea-portValidation test cleanup failed: ' + $portValidationFile) }
            $portValidationRemoved.Add($portValidationName)
        }
    }
    if ((Test-Path -LiteralPath $portValidationBuild) -and @(Get-ChildItem -LiteralPath $portValidationBuild -Force).Count -eq 0) {
        Remove-Item -LiteralPath $portValidationBuild
    }
    if (Test-Path -LiteralPath $portValidationOutput) {
        [pscustomobject]@{ ReleasedBytes = $portValidationFreed; RemovedFiles = @($portValidationRemoved); EvidenceRetained = $true;
            MainProjectCleaned = $false; SharedNUnitRetained = $portValidationNunit } |
            ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $portValidationOutput 'cleanup.json') -Encoding utf8
    }
    Write-Output ('Cleaned task test binaries: ' + $portValidationFreed + ' bytes; logs and results retained.')
}