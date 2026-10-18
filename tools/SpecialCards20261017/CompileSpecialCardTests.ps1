$ErrorActionPreference = 'Stop'
$specialWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$specialOutput = Join-Path $specialWorkspace 'Artifacts/SpecialCardRedesign20261017/UiCompile'
$specialUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$specialTestRsp = @([IO.File]::ReadAllLines((Join-Path $specialOutput 'Presentation.rsp')) |
    Where-Object { $_ -notmatch '^"?[^-/].*\.cs"?$' -and $_ -notmatch '^-(out|refout|analyzer):' -and $_ -notmatch '^/additionalfile:' })
$specialTestRsp += '-out:"' + (Join-Path $specialOutput 'SpecialCardCatalogTests.dll').Replace('\','/') + '"'
$specialTestRsp += '-r:"' + (Join-Path $specialOutput 'ShatteredSail.Presentation.ref.dll').Replace('\','/') + '"'
$specialNUnit = Get-ChildItem -Path (Join-Path $specialWorkspace 'UnityClient/Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll') | Select-Object -First 1
if (-not $specialNUnit) { throw 'Unity NUnit reference not found.' }
$specialTestRsp += '-r:"' + $specialNUnit.FullName.Replace('\','/') + '"'
$specialTestRsp += '"' + (Join-Path $specialWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/SpecialCardCatalogTests.cs').Replace('\','/') + '"'
$specialRspPath = Join-Path $specialOutput 'SpecialCardCatalogTests.rsp'
[IO.File]::WriteAllLines($specialRspPath, $specialTestRsp, [Text.UTF8Encoding]::new($false))
Push-Location (Join-Path $specialWorkspace 'UnityClient')
try {
    & (Join-Path $specialUnity 'NetCoreRuntime/dotnet.exe') (Join-Path $specialUnity 'DotNetSdkRoslyn/csc.dll') ('@' + $specialRspPath) 2>&1 |
        Tee-Object -FilePath (Join-Path $specialOutput 'SpecialCardCatalogTests.log')
    if ($LASTEXITCODE -ne 0) { throw 'SpecialCardCatalogTests compilation failed.' }
    Write-Output 'PASS SpecialCardCatalogTests compilation'
} finally { Pop-Location }
