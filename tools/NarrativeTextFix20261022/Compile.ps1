param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string[]]$TestNames = @('ComponentEnvironmentalEffectsTests.cs'),
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$narrativeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$narrativeOutput = Join-Path $narrativeWorkspace 'Artifacts/NarrativeTextFix20261022/Compile'
$narrativeMain = Join-Path $narrativeWorkspace 'UnityClient'
$narrativeCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$narrativeDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
foreach ($narrativeRequired in @($narrativeCompiler, $narrativeDotnet)) {
    if (-not (Test-Path -LiteralPath $narrativeRequired -PathType Leaf)) { throw ('Missing compiler dependency: ' + $narrativeRequired) }
}
New-Item -ItemType Directory -Force -Path $narrativeOutput | Out-Null
Push-Location $narrativeMain
try {
    foreach ($narrativeAssembly in @('Core', 'Content', 'Persistence', 'Presentation')) {
        $narrativeReference = Join-Path $ReferenceResponses ($narrativeAssembly + '.rsp')
        if (-not (Test-Path -LiteralPath $narrativeReference -PathType Leaf)) { throw ('Missing reference response: ' + $narrativeReference) }
        $narrativeResponse = [IO.File]::ReadAllLines($narrativeReference) |
            ForEach-Object { $_.Replace($ReferenceResponses.Replace('\','/'), $narrativeOutput.Replace('\','/')) }
        $narrativeSources = Join-Path $narrativeMain ('Assets/_Project/' + $narrativeAssembly + '/Runtime')
        $narrativeSourcePattern = '^"?(?:D:/Game/UnityClient/)?Assets/_Project/' + $narrativeAssembly + '/Runtime/.*\.cs"?$'
        $narrativeResponse = @($narrativeResponse | Where-Object { $_ -notmatch $narrativeSourcePattern })
        $narrativeResponse += @(Get-ChildItem -LiteralPath $narrativeSources -File -Filter '*.cs' -Recurse |
            Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
        $narrativeResponsePath = Join-Path $narrativeOutput ($narrativeAssembly + '.rsp')
        [IO.File]::WriteAllLines($narrativeResponsePath, $narrativeResponse, [Text.UTF8Encoding]::new($false))
        & $narrativeDotnet $narrativeCompiler ('@' + $narrativeResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $narrativeOutput ($narrativeAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: ' + $narrativeAssembly) }
        Write-Output ('PASS ' + $narrativeAssembly)
    }
    $narrativeTestResponse = @($narrativeResponse | Where-Object { $_ -notmatch '\.cs"?$' -and $_ -notmatch '^-(out|refout):' })
    $narrativeTestResponse += '-r:"' + (Join-Path $narrativeOutput 'ShatteredSail.Presentation.ref.dll').Replace('\','/') + '"'
    $narrativeNunit = Get-ChildItem -LiteralPath (Join-Path $narrativeMain 'Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
    if (-not $narrativeNunit) { throw 'Installed Unity NUnit package not found.' }
    $narrativeTestResponse += '-r:"' + (Join-Path $narrativeNunit.FullName 'net40/unity-custom/nunit.framework.dll').Replace('\','/') + '"'
    $narrativeTestResponse += '-out:"' + (Join-Path $narrativeOutput 'NarrativeComponentTests.dll').Replace('\','/') + '"'
    foreach ($narrativeTestName in $TestNames) {
        if ([IO.Path]::GetFileName($narrativeTestName) -ne $narrativeTestName -or -not $narrativeTestName.EndsWith('.cs')) { throw ('Expected an EditMode test filename: ' + $narrativeTestName) }
        $narrativeTestPath = Join-Path $narrativeMain ('Assets/_Project/Tests/EditMode/' + $narrativeTestName)
        if (-not (Test-Path -LiteralPath $narrativeTestPath -PathType Leaf)) { throw ('Missing test source: ' + $narrativeTestPath) }
        $narrativeTestResponse += '"' + $narrativeTestPath.Replace('\','/') + '"'
    }
    $narrativeTestResponsePath = Join-Path $narrativeOutput 'Tests.rsp'
    [IO.File]::WriteAllLines($narrativeTestResponsePath, $narrativeTestResponse, [Text.UTF8Encoding]::new($false))
    & $narrativeDotnet $narrativeCompiler ('@' + $narrativeTestResponsePath) 2>&1 |
        Tee-Object -FilePath (Join-Path $narrativeOutput 'Tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed: narrative component tests' }
    Write-Output 'PASS narrative component test compilation'
} finally { Pop-Location }
