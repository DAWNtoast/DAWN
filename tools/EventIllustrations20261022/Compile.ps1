param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [switch]$UseImportedDependencies
)
$ErrorActionPreference = 'Stop'
$eventWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$eventOutput = Join-Path $eventWorkspace 'Artifacts/EventIllustrations20261022/Compile'
$eventUnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $eventOutput | Out-Null
Push-Location (Join-Path $eventWorkspace 'UnityClient')
try {
    $eventAssemblies = if ($UseImportedDependencies) { @('Presentation') } else { @('Core','Content','Persistence','Presentation') }
    foreach ($eventAssembly in $eventAssemblies) {
        $eventResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($eventAssembly + '.rsp'))) |
            ForEach-Object { $_.Replace($ReferenceResponses.Replace('\','/'), $eventOutput.Replace('\','/')) }
        if ($UseImportedDependencies) {
            foreach ($eventDependency in @('Core','Content','Persistence')) {
                $eventCachedReference = (Join-Path $eventOutput ('ShatteredSail.' + $eventDependency + '.ref.dll')).Replace('\','/')
                $eventImportedReference = (Join-Path $eventWorkspace ('UnityClient/Library/ScriptAssemblies/ShatteredSail.' + $eventDependency + '.dll')).Replace('\','/')
                $eventResponse = @($eventResponse | ForEach-Object { $_.Replace($eventCachedReference, $eventImportedReference) })
            }
        }
        $eventSources = Join-Path $eventWorkspace ('UnityClient/Assets/_Project/' + $eventAssembly + '/Runtime')
        $eventSourcePattern = '^"?(?:D:/Game/UnityClient/)?Assets/_Project/' + $eventAssembly + '/Runtime/.*\.cs"?$'
        $eventResponse = @($eventResponse | Where-Object { $_ -notmatch $eventSourcePattern })
        $eventResponse += @(Get-ChildItem -LiteralPath $eventSources -File -Filter '*.cs' -Recurse |
            Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
        $eventResponsePath = Join-Path $eventOutput ($eventAssembly + '.rsp')
        [IO.File]::WriteAllLines($eventResponsePath, $eventResponse, [Text.UTF8Encoding]::new($false))
        & (Join-Path $eventUnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $eventUnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $eventResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $eventOutput ($eventAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: ' + $eventAssembly) }
        Write-Output ('PASS ' + $eventAssembly)
    }
    $eventTestResponse = @($eventResponse | Where-Object { $_ -notmatch '\.cs"?$' -and $_ -notmatch '^-(out|refout):' })
    $eventTestResponse += '-r:"' + (Join-Path $eventOutput 'ShatteredSail.Presentation.ref.dll').Replace('\','/') + '"'
    $eventNunit = Get-ChildItem -LiteralPath (Join-Path $eventWorkspace 'UnityClient/Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
    if (-not $eventNunit) { throw 'Installed Unity NUnit package not found.' }
    $eventTestResponse += '-r:"' + (Join-Path $eventNunit.FullName 'net40/unity-custom/nunit.framework.dll').Replace('\','/') + '"'
    $eventTestResponse += '-out:"' + (Join-Path $eventOutput 'EncounterIllustrationTests.dll').Replace('\','/') + '"'
    foreach ($eventTestName in @('EncounterIllustrationCatalogTests.cs','ArtCatalogLazyLoadingTests.cs','BossEncounterSceneTests.cs')) {
        $eventTestResponse += '"' + (Join-Path $eventWorkspace ('UnityClient/Assets/_Project/Tests/EditMode/' + $eventTestName)).Replace('\','/') + '"'
    }
    $eventTestResponsePath = Join-Path $eventOutput 'Tests.rsp'
    [IO.File]::WriteAllLines($eventTestResponsePath, $eventTestResponse, [Text.UTF8Encoding]::new($false))
    & (Join-Path $eventUnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $eventUnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $eventTestResponsePath) 2>&1 |
        Tee-Object -FilePath (Join-Path $eventOutput 'Tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed: encounter illustration tests' }
    Write-Output 'PASS Encounter illustration and lazy-loading test compilation'
} finally { Pop-Location }
