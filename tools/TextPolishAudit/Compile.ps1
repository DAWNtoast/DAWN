param([string[]]$Stages = @('Core','Content','Presentation','Edit','Play','Editor'))
$ErrorActionPreference = 'Stop'
$taskOutput = 'D:/Game/Artifacts/TextPolish20261008/static-root'
$taskProject = 'D:/Game/tmp/第七港口v2-build-project'
$taskDag = $taskProject + '/Library/Bee/artifacts/1900b0aE.dag'
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskSpecs = @{
    Core = @('ShatteredSail.Core','Core/Runtime')
    Content = @('ShatteredSail.Content','Content/Runtime')
    Presentation = @('ShatteredSail.Presentation','Presentation/Runtime')
    Edit = @('ShatteredSail.Core.Tests','Tests/EditMode')
    Play = @('ShatteredSail.Presentation.PlayModeTests','Tests/PlayMode')
    Editor = @('ShatteredSail.Editor','Editor')
}
foreach ($taskStage in $Stages) {
    $taskSpec = $taskSpecs[$taskStage]
    $taskAssembly = $taskSpec[0]
    $taskLines = @()
    foreach ($taskLineOriginal in Get-Content -LiteralPath ($taskDag + '/' + $taskAssembly + '.rsp')) {
        if ($taskLineOriginal -match '^".*\.cs"$' -or $taskLineOriginal -match '^[-/](out|refout):') { continue }
        $taskLine = $taskLineOriginal.Replace('"Library/', ('"' + $taskProject + '/Library/')).Replace('"Packages/', ('"' + $taskProject + '/Packages/'))
        foreach ($taskReference in @('ShatteredSail.Core','ShatteredSail.Content','ShatteredSail.Presentation')) {
            if ($taskLine -match ('^[-/]r:.*' + [regex]::Escape($taskReference) + '\.ref\.dll"$')) { $taskLine = '-r:"' + $taskOutput + '/' + $taskReference + '.ref.dll"' }
        }
        $taskLines += $taskLine
    }
    $taskLines += '-out:"' + $taskOutput + '/' + $taskAssembly + '.dll"'
    $taskLines += '-refout:"' + $taskOutput + '/' + $taskAssembly + '.ref.dll"'
    $taskLines += @(Get-ChildItem -LiteralPath ('D:/Game/UnityClient/Assets/_Project/' + $taskSpec[1]) -Filter '*.cs' -Recurse | Sort-Object FullName | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($taskStage -eq 'Editor') { $taskLines += @(Get-ChildItem -LiteralPath 'D:/Game/Tools/TextPolishAudit' -Filter '*.cs' | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' }) }
    $taskResponse = $taskOutput + '/' + $taskStage + '.rsp'
    [IO.File]::WriteAllLines($taskResponse, $taskLines, [Text.UTF8Encoding]::new($false))
    & 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@' + $taskResponse) 2>&1 | Tee-Object -FilePath ($taskOutput + '/' + $taskStage + '-compile.log')
    if ($LASTEXITCODE -ne 0) { throw "$taskStage static compilation failed: $LASTEXITCODE" }
    Write-Output "$taskStage static compilation passed."
}
