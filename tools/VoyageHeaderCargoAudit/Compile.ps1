param([string[]]$Stages = @('Core', 'Presentation', 'Edit', 'Editor'))
$ErrorActionPreference = 'Stop'
$root = 'D:/Game/Artifacts/VoyageHeaderCargo20261008/static'
$project = 'D:/Game/tmp/第七港口v2-build-project'
$dag = $project + '/Library/Bee/artifacts/1900b0aE.dag'
[IO.Directory]::CreateDirectory($root) | Out-Null
$specs = @{
    Core = @('ShatteredSail.Core', 'Core/Runtime')
    Presentation = @('ShatteredSail.Presentation', 'Presentation/Runtime')
    Edit = @('ShatteredSail.Core.Tests', 'Tests/EditMode')
    Editor = @('ShatteredSail.Editor', 'Editor')
}
foreach ($stage in $Stages) {
    $spec = $specs[$stage]
    if ($null -eq $spec) { throw "Unknown stage: $stage" }
    $assembly = $spec[0]
    $lines = @()
    foreach ($line in Get-Content -LiteralPath ($dag + '/' + $assembly + '.rsp')) {
        if ($line -match '^".*\.cs"$' -or $line -match '^[-/](out|refout):') { continue }
        $line = $line.Replace('"Library/', ('"' + $project + '/Library/')).Replace('"Packages/', ('"' + $project + '/Packages/'))
        if ($line -match '^[-/]r:.*ShatteredSail.Core\.ref\.dll"$') { $line = '-r:"' + $root + '/ShatteredSail.Core.ref.dll"' }
        if ($line -match '^[-/]r:.*ShatteredSail.Presentation\.ref\.dll"$') { $line = '-r:"' + $root + '/ShatteredSail.Presentation.ref.dll"' }
        $lines += $line
    }
    $lines += '-out:"' + $root + '/' + $assembly + '.dll"'
    $lines += '-refout:"' + $root + '/' + $assembly + '.ref.dll"'
    $lines += @(Get-ChildItem -LiteralPath ('D:/Game/UnityClient/Assets/_Project/' + $spec[1]) -Filter '*.cs' -Recurse |
        Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($stage -eq 'Editor') { $lines += '"D:/Game/Tools/VoyageHeaderCargoAudit/VoyageHeaderPreviewSceneSync.cs"' }
    $response = $root + '/' + $stage + '.rsp'
    [IO.File]::WriteAllLines($response, $lines, [Text.UTF8Encoding]::new($false))
    & 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@' + $response) 2>&1 |
        Tee-Object -FilePath ($root + '/' + $stage + '-compile.log')
    if ($LASTEXITCODE -ne 0) { throw "$stage static compilation failed: $LASTEXITCODE" }
    Write-Output "$stage static compilation passed."
}
