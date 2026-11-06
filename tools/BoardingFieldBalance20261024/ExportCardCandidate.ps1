param(
    [string]$CoreSource='D:/Game/outputs/boarding-field-balance-20261024/experiments/card-a-core',
    [string]$CardOverlay='D:/Game/outputs/boarding-field-balance-20261024/experiments/card-a',
    [string]$Output='D:/Game/outputs/boarding-field-balance-20261024/card-a-catalog'
)
$ErrorActionPreference='Stop'
$cardWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cardUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$cardSources=@((Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs').FullName)
$cardBaseExporter=Get-Content -LiteralPath (Join-Path $cardWorkspace 'tools/CardListBySchool20261023/ExportCardList.cs') -Raw
$cardBaseExporter=$cardBaseExporter.Replace('.Contains("生命值 2")','.Contains("生命值 4")')
$cardBaseExporterPath=Join-Path $Output 'ExportCardList.cs'
$cardBaseExporter | Set-Content -LiteralPath $cardBaseExporterPath -Encoding utf8
$cardSources+=$cardBaseExporterPath
$cardSources+=@('tools/SchoolUpgradeFocus20261024/ExportUpgradeList.cs','tools/SpecialCards20261017/CatalogResourceCosts.cs' | ForEach-Object {Join-Path $cardWorkspace $_})
foreach($cardName in @('CardSpecialCostPresentation.cs','CardSupplyCostPresentation.cs','CardOrnamentKind.cs')){
    $cardOverride=Join-Path $CardOverlay $cardName
    $cardSources+=if(Test-Path -LiteralPath $cardOverride){$cardOverride}else{Join-Path $cardWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/'+$cardName)}
}
foreach($cardMain in @('ExportCardList','ExportUpgradeList')){
    $cardExe=Join-Path $Output ($cardMain+'.exe')
    $cardArgs=@("$cardUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe',"-main:$cardMain",'-langversion:9','-noconfig','-nostdlib',"-out:$cardExe")
    foreach($cardAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll')){$cardArgs+="-r:$cardUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$cardAssembly"}
    $cardArgs+=$cardSources
    & "$cardUnity/NetCoreRuntime/dotnet.exe" @cardArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output ($cardMain+'-compile.log'))
    if($LASTEXITCODE -ne 0){throw 'Candidate exporter compilation failed'}
    $cardRunArgs=@($cardExe,$Output)
    if($cardMain -eq 'ExportUpgradeList'){$cardRunArgs+=Join-Path $Output 'card-list-data.json'}
    & "$cardUnity/MonoBleedingEdge/bin/mono.exe" @cardRunArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output ($cardMain+'-export.log'))
    if($LASTEXITCODE -ne 0){throw 'Candidate catalogue invariants failed'}
}
$cardBefore=Get-Content -LiteralPath (Join-Path $cardWorkspace 'outputs/boarding-field-balance-20261024/Inputs/Catalog/card-upgrade-data.json') -Raw | ConvertFrom-Json
$cardAfter=Get-Content -LiteralPath (Join-Path $Output 'card-upgrade-data.json') -Raw | ConvertFrom-Json
$cardBeforeIndex=@{}
foreach($cardEntry in $cardBefore.Cards){foreach($cardStage in $cardEntry.Stages){if($cardStage.Stage -ne '接舷'){$cardBeforeIndex[$cardEntry.Id+'|'+$cardStage.Stage]=$cardStage | ConvertTo-Json -Depth 20 -Compress}}}
$cardDiffs=@(foreach($cardEntry in $cardAfter.Cards){foreach($cardStage in $cardEntry.Stages){if($cardStage.Stage -eq '接舷'){continue};$cardKey=$cardEntry.Id+'|'+$cardStage.Stage;$cardNow=$cardStage|ConvertTo-Json -Depth 20 -Compress;if($cardBeforeIndex[$cardKey] -ne $cardNow){$cardKey}}})
$cardDiffs | Set-Content -LiteralPath (Join-Path $Output 'non-boarding-differences.txt') -Encoding utf8
if($cardDiffs.Count -gt 0){throw ('Preparation/naval variants unexpectedly changed: '+($cardDiffs -join ','))}
Write-Output ('PASS: all '+$cardBeforeIndex.Count+' preparation/naval stage variants unchanged at every rank')
