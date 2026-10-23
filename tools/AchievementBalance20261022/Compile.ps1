param(
    [string]$RunTag = 'current',
    [string]$ReferenceResponses = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$achievementWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$achievementMain = Join-Path $achievementWorkspace 'UnityClient'
$achievementOutput = Join-Path $achievementWorkspace ('Artifacts/AchievementBalance20261022/' + $RunTag + '/Compile')
New-Item -ItemType Directory -Force -Path $achievementOutput | Out-Null
$achievementAssemblySources = [ordered]@{ 'Core'='Core/Runtime'; 'Content'='Content/Runtime'; 'Persistence'='Persistence/Runtime'; 'Presentation'='Presentation/Runtime'; 'Core.Tests'='Tests/EditMode'; 'Presentation.PlayModeTests'='Tests/PlayMode' }
$achievementHashes = foreach ($achievementSources in $achievementAssemblySources.Values) {
    Get-ChildItem -LiteralPath (Join-Path $achievementMain ('Assets/_Project/' + $achievementSources)) -File -Filter '*.cs' -Recurse |
        ForEach-Object { [pscustomobject]@{ Source=$_.FullName; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash } }
}
$achievementHashes | Export-Csv -LiteralPath (Join-Path $achievementOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Push-Location $achievementMain
try {
    foreach ($achievementAssembly in $achievementAssemblySources.Keys) {
        $achievementResponse = @([IO.File]::ReadAllLines((Join-Path $ReferenceResponses ('ShatteredSail.' + $achievementAssembly + '.rsp'))) |
            Where-Object { $_ -notmatch '\.cs"?$' -and $_ -notmatch '^-(out|refout):' })
        foreach ($achievementDependency in @('Core','Content','Persistence','Presentation')) {
            $achievementReference = '-r:"' + (Join-Path $achievementOutput ('ShatteredSail.' + $achievementDependency + '.ref.dll')).Replace('\','/') + '"'
            $achievementResponse = @($achievementResponse | ForEach-Object {
                if ($_ -match ('^-r:.*[/\\]ShatteredSail\.' + [regex]::Escape($achievementDependency) + '\.ref\.dll"?$')) { $achievementReference } else { $_ }
            })
        }
        $achievementResponse += '-out:"' + (Join-Path $achievementOutput ('ShatteredSail.' + $achievementAssembly + '.dll')).Replace('\','/') + '"'
        $achievementResponse += '-refout:"' + (Join-Path $achievementOutput ('ShatteredSail.' + $achievementAssembly + '.ref.dll')).Replace('\','/') + '"'
        $achievementSourceDirectory = Join-Path $achievementMain ('Assets/_Project/' + $achievementAssemblySources[$achievementAssembly])
        $achievementResponse += @(Get-ChildItem -LiteralPath $achievementSourceDirectory -File -Filter '*.cs' -Recurse |
            Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
        $achievementResponsePath = Join-Path $achievementOutput ($achievementAssembly + '.rsp')
        [IO.File]::WriteAllLines($achievementResponsePath, $achievementResponse, [Text.UTF8Encoding]::new($false))
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $achievementResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $achievementOutput ($achievementAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: ' + $achievementAssembly) }
        Write-Output ('PASS ' + $achievementAssembly)
    }
    foreach ($achievementHash in $achievementHashes) {
        if ((Get-FileHash -LiteralPath $achievementHash.Source).Hash -ne $achievementHash.SHA256) { throw ('Source changed during compilation: ' + $achievementHash.Source) }
    }
} finally { Pop-Location }
