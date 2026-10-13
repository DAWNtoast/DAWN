param([string[]]$Stages = @('Core','Content','Persistence','Presentation','Edit','Play'))
$ErrorActionPreference = 'Stop'
$taskOutput = 'D:/Game/Artifacts/ShipyardEquipment20261010/static'
$taskProject = 'D:/Game/tmp/prefab-ui-20261009'
$taskDag = $taskProject + '/Library/Bee/artifacts/1900b0aE.dag'
$taskSource = 'D:/Game/UnityClient/Assets/_Project/'
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskSpecs = @{
    Core = @('ShatteredSail.Core','Core/Runtime')
    Content = @('ShatteredSail.Content','Content/Runtime')
    Persistence = @('ShatteredSail.Persistence','Persistence/Runtime')
    Presentation = @('ShatteredSail.Presentation','Presentation/Runtime')
    Edit = @('ShatteredSail.Core.Tests','Tests/EditMode')
    Play = @('ShatteredSail.Presentation.PlayModeTests','Tests/PlayMode')
    Editor = @('ShatteredSail.Editor','Editor')
}
foreach ($taskStage in $Stages) {
    if (-not $taskSpecs.ContainsKey($taskStage)) { throw "Unknown compile stage: $taskStage" }
    $taskSpec = $taskSpecs[$taskStage]
    $taskAssembly = $taskSpec[0]
    $taskLines = @()
    foreach ($taskLineOriginal in Get-Content -LiteralPath ($taskDag + '/' + $taskAssembly + '.rsp')) {
        if ($taskLineOriginal -match '^".*\.cs"$' -or $taskLineOriginal -match '^[-/](out|refout):') { continue }
        $taskLine = $taskLineOriginal.Replace('"Library/', ('"' + $taskProject + '/Library/')).Replace('"Packages/', ('"' + $taskProject + '/Packages/'))
        foreach ($taskReference in @('ShatteredSail.Core','ShatteredSail.Content','ShatteredSail.Persistence','ShatteredSail.Presentation')) {
            if ($taskLine -match ('^[-/]r:.*' + [regex]::Escape($taskReference) + '\.ref\.dll"$')) {
                $taskLine = '-r:"' + $taskOutput + '/' + $taskReference + '.ref.dll"'
            }
        }
        $taskLines += $taskLine
    }
    $taskLines += '-out:"' + $taskOutput + '/' + $taskAssembly + '.dll"'
    $taskLines += '-refout:"' + $taskOutput + '/' + $taskAssembly + '.ref.dll"'
    $taskLines += @(Get-ChildItem -LiteralPath ($taskSource + $taskSpec[1]) -Filter '*.cs' -Recurse | Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    $taskResponse = $taskOutput + '/' + $taskStage + '.rsp'
    [IO.File]::WriteAllLines($taskResponse, $taskLines, [Text.UTF8Encoding]::new($false))
    & 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@' + $taskResponse) 2>&1 | Tee-Object -FilePath ($taskOutput + '/' + $taskStage + '-compile.log')
    if ($LASTEXITCODE -ne 0) { throw "$taskStage static compilation failed: $LASTEXITCODE" }
    Write-Output "$taskStage static compilation passed."
}
