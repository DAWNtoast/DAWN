param([string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile')
$ErrorActionPreference = 'Stop'
$specialWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$specialUiOutput = Join-Path $specialWorkspace 'Artifacts/SpecialCardRedesign20261017/UiCompile'
$specialUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $specialUiOutput | Out-Null
Push-Location (Join-Path $specialWorkspace 'UnityClient')
try {
    foreach ($specialAssembly in @('Core','Content','Persistence','Presentation')) {
        $specialRsp = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($specialAssembly + '.rsp'))) |
            ForEach-Object { $_.Replace($ReferenceResponses.Replace('\','/'), $specialUiOutput.Replace('\','/')) }
        $specialSourceRoot = Join-Path $specialWorkspace ('UnityClient/Assets/_Project/' + $specialAssembly + '/Runtime')
        if (Test-Path -LiteralPath $specialSourceRoot) {
            $specialSourcePattern = '^"?(?:D:/Game/UnityClient/)?Assets/_Project/' + $specialAssembly + '/Runtime/.*\.cs"?$'
            $specialRsp = @($specialRsp | Where-Object { $_ -notmatch $specialSourcePattern })
            $specialRsp += @(Get-ChildItem -LiteralPath $specialSourceRoot -Filter '*.cs' -Recurse |
                Sort-Object Name | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
        }
        $specialRspPath = Join-Path $specialUiOutput ($specialAssembly + '.rsp')
        [IO.File]::WriteAllLines($specialRspPath, $specialRsp, [Text.UTF8Encoding]::new($false))
        & (Join-Path $specialUnity 'NetCoreRuntime/dotnet.exe') (Join-Path $specialUnity 'DotNetSdkRoslyn/csc.dll') ('@' + $specialRspPath) 2>&1 |
            Tee-Object -FilePath (Join-Path $specialUiOutput ($specialAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: ' + $specialAssembly) }
        Write-Output ('PASS ' + $specialAssembly)
    }
} finally { Pop-Location }
