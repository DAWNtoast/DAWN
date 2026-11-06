param(
    [string]$ProductionCompile = 'D:/Game/Artifacts/BoardingFieldBalance20261024/Compile',
    [string]$Output = 'D:/Game/Artifacts/NewCardArtRelease20261024/IntegrationCompile'
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskProject = Join-Path $taskWorkspace 'UnityClient'
$taskOutput = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $taskOutput) { throw 'Use a fresh integration compile output directory.' }
$taskNunit = @(Get-ChildItem -Path (Join-Path $taskProject 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll') -File)
if ($taskNunit.Count -ne 1) { throw 'Exactly one project NUnit reference is required.' }
$taskResponse = @([IO.File]::ReadAllLines((Join-Path $ProductionCompile 'Presentation.rsp')) | Where-Object {
    $_ -notmatch '^-(out|refout|analyzer):' -and $_ -notmatch '^"?.*\.cs"?$'
})
$taskResponse += '-out:"' + (Join-Path $taskOutput 'NewCardDedicatedArtTests.dll').Replace('\','/') + '"'
$taskResponse += '-r:"' + (Join-Path $ProductionCompile 'ShatteredSail.Presentation.dll').Replace('\','/') + '"'
$taskResponse += '-r:"' + $taskNunit[0].FullName.Replace('\','/') + '"'
$taskSource = Join-Path $taskProject 'Assets/_Project/Tests/EditMode/NewCardDedicatedArt20261024Tests.cs'
$taskResponse += '"' + $taskSource.Replace('\','/') + '"'
$taskHash = (Get-FileHash -LiteralPath $taskSource).Hash
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskResponsePath = Join-Path $taskOutput 'Tests.rsp'
[IO.File]::WriteAllLines($taskResponsePath,$taskResponse,[Text.UTF8Encoding]::new($false))
Push-Location $taskProject
try {
    & 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@' + $taskResponsePath) 2>&1 |
        Tee-Object -FilePath (Join-Path $taskOutput 'compile.log')
    if ($LASTEXITCODE -ne 0) { throw 'New-card resource regression fixture did not compile.' }
} finally { Pop-Location }
if ((Get-FileHash -LiteralPath $taskSource).Hash -ne $taskHash) { throw 'Test source changed during compilation.' }
[pscustomobject]@{ Result='PASS'; Source=$taskSource; SourceSHA256=$taskHash; Scope='C# fixture compilation only; Unity runtime/resource tests run after image installation.' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'validation.json') -Encoding utf8
Write-Output 'PASS: resource regression fixture compiles. No Unity Editor or preview generator launched.'
