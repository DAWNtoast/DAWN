param([switch]$VerifyOnly)
$ErrorActionPreference='Stop'
$tideRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tideOutput=Join-Path $tideRoot 'outputs/tide-component-duration-20261026'
$tideCatalog=Join-Path $tideRoot 'outputs/card-list-by-school-20261023'
$tideBefore=Join-Path $tideOutput 'BeforeCatalog'
$tideExpected=@{
    card_special_sail_ghostcurrent_canvas=@(2,4,6)
    card_special_sail_tempest_wing=@(5,8,10,13)
}
function Compact-TideText([string]$Value){[regex]::Replace([Net.WebUtility]::HtmlDecode([regex]::Replace($Value,'<[^>]*>','')), '\s+', '')}
function Compare-TideCards($Before,$After,[bool]$Upgraded){
    if($Before.Cards.Count -ne $After.Cards.Count){throw 'Published card count changed'}
    $changed=[Collections.Generic.List[string]]::new()
    foreach($card in $After.Cards){
        $prior=@($Before.Cards|Where-Object Id -eq $card.Id)
        if($prior.Count -ne 1){throw "Identity absent or duplicated: $($card.Id)"}
        $oldJson=$prior[0]|ConvertTo-Json -Depth 100 -Compress
        $newJson=$card|ConvertTo-Json -Depth 100 -Compress
        if($oldJson -ne $newJson){$changed.Add($card.Id)}
        if(!$tideExpected.ContainsKey($card.Id)){
            if($oldJson -ne $newJson){throw "Unexpected card change: $($card.Id)"}
            continue
        }
        if([regex]::Replace($oldJson,'延长 \d+ 回合','延长 N 回合') -ne [regex]::Replace($newJson,'延长 \d+ 回合','延长 N 回合')){
            throw "Non-duration attribute changed: $($card.Id)"
        }
        $levels=if($Upgraded){@($card.Stages|ForEach-Object Levels)}else{@($card.StageVariants)}
        $wanted=if($Upgraded){$tideExpected[$card.Id]}else{@($tideExpected[$card.Id][0])}
        if($levels.Count -ne $wanted.Count){throw "Rank/variant count changed: $($card.Id)"}
        for($i=0;$i -lt $wanted.Count;$i++){
            if($levels[$i].Effect -notmatch ('使所有己方定向水流延长 '+$wanted[$i]+' 回合')){throw "Wrong final duration: $($card.Id) rank $i"}
        }
    }
    if((($changed|Sort-Object) -join ',') -ne (($tideExpected.Keys|Sort-Object) -join ',')){throw 'Expected exactly two changed cards'}
    return @($changed)
}
if(!(Test-Path -LiteralPath (Join-Path $tideBefore 'card-list-data.json'))){throw 'Backup of prior catalogue required before export'}
$tideTextHash=(Get-FileHash -LiteralPath (Join-Path $tideBefore '全部卡牌清单.txt')).Hash
try {
    if(!$VerifyOnly){& (Join-Path $tideRoot 'tools/CardListBySchool20261023/ExportCardList.ps1') -Output $tideCatalog}
    $tideOld=Get-Content -LiteralPath (Join-Path $tideBefore 'card-list-data.json') -Raw | ConvertFrom-Json
    $tideNew=Get-Content -LiteralPath (Join-Path $tideCatalog 'card-list-data.json') -Raw | ConvertFrom-Json
    $tideOldUp=Get-Content -LiteralPath (Join-Path $tideBefore 'card-upgrade-data.json') -Raw | ConvertFrom-Json
    $tideNewUp=Get-Content -LiteralPath (Join-Path $tideCatalog 'card-upgrade-data.json') -Raw | ConvertFrom-Json
    $tideBaseChanged=@(Compare-TideCards $tideOld $tideNew $false)
    $tideUpgradeChanged=@(Compare-TideCards $tideOldUp $tideNewUp $true)
    foreach($name in @('Glossary','Rules','StageCount','CrewCount','ComponentCount','Basis')){
        if(($tideOld.$name|ConvertTo-Json -Depth 100 -Compress) -ne ($tideNew.$name|ConvertTo-Json -Depth 100 -Compress)){throw "Base catalogue metadata changed: $name"}
    }
    foreach($name in @('Rules','CardCount','StageCount','LevelCount','Basis')){
        if(($tideOldUp.$name|ConvertTo-Json -Depth 100 -Compress) -ne ($tideNewUp.$name|ConvertTo-Json -Depth 100 -Compress)){throw "Upgrade catalogue metadata changed: $name"}
    }
    $tideHtml=Get-Content -LiteralPath (Join-Path $tideCatalog '全部卡牌清单.html') -Raw
    $tideUpHtml=Get-Content -LiteralPath (Join-Path $tideCatalog '全部卡牌强化清单.html') -Raw
    foreach($id in $tideExpected.Keys){
        $card=$tideNew.Cards|Where-Object Id -eq $id
        $block=[regex]::Match($tideHtml,'(?s)<tbody class="card-block" data-card-id="'+[regex]::Escape($id)+'".*?</tbody>').Value
        if(!$block){throw "Missing HTML card block: $id"}
        foreach($variant in $card.StageVariants){if(!(Compact-TideText $block).Contains((Compact-TideText $variant.Effect))){throw "Base HTML differs: $id"}}
        $up=$tideNewUp.Cards|Where-Object Id -eq $id
        $article=[regex]::Match($tideUpHtml,'(?s)<article id="card-'+$card.Number+'">.*?</article>').Value
        if(!$article){throw "Missing upgrade HTML card block: $id"}
        foreach($level in @($up.Stages|ForEach-Object Levels)){if(!(Compact-TideText $article).Contains((Compact-TideText $level.Effect))){throw "Upgrade HTML differs: $id rank $($level.Level)"}}
    }
    if((Get-FileHash -LiteralPath (Join-Path $tideCatalog '全部卡牌清单.txt')).Hash -ne $tideTextHash){throw 'Existing manuscript TXT was modified'}
    foreach($hashFile in @('source-hashes.csv','upgrade-source-hashes.csv')){
        foreach($source in (Import-Csv -LiteralPath (Join-Path $tideCatalog $hashFile))){
            if((Get-FileHash -LiteralPath $source.Source).Hash -ne $source.SHA256){throw "Source changed during export: $($source.Source)"}
        }
    }
    [pscustomobject]@{Result='PASS';PublishedCards=$tideNew.Cards.Count;StageDefinitions=$tideNew.StageCount;
        LegalUpgradeRows=$tideNewUp.LevelCount;BaseChangedIds=$tideBaseChanged;UpgradeChangedIds=$tideUpgradeChanged;
        EpicDuration=@(2,4,6);LegendaryDuration=@(5,8,10,13);NonDurationAttributesUnchanged=$true;
        OtherCardsUnchanged=$true;HtmlMatchesFinalDefinitions=$true;ManuscriptTxtUnchanged=$true;FixedHtml=(Join-Path $tideCatalog '全部卡牌清单.html')} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $tideOutput 'catalog-verification.json') -Encoding utf8
    Write-Output 'PASS: final catalogues and HTML agree; exactly two cards changed, only field-extension durations.'
} finally {
    foreach($path in @($tideRoot,(Join-Path $tideRoot 'outputs'),$tideCatalog,(Join-Path $tideCatalog 'Build'),(Join-Path $tideCatalog 'UpgradeBuild'))){
        if((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Cleanup refused directory link: $path"}
    }
    $processes=@(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe' OR Name='Unity.exe'"|Where-Object {$_.CommandLine -and ($_.CommandLine.Contains($tideCatalog) -or $_.CommandLine.Contains($tideCatalog.Replace('\','/')))})
    if($processes.Count){throw 'Catalogue compile output still in use; cleanup deferred'}
    $freed=0L
    foreach($pair in @(@('Build','ExportCardList.exe'),@('UpgradeBuild','ExportUpgradeList.exe'))){
        $dir=Join-Path $tideCatalog $pair[0];$file=[IO.Path]::GetFullPath((Join-Path $dir $pair[1]))
        if([IO.Path]::GetDirectoryName($file) -ne [IO.Path]::GetFullPath($dir)){throw 'Unexpected cleanup target'}
        if(Test-Path -LiteralPath $file){
            $item=Get-Item -LiteralPath $file
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Cleanup refused file link: $file"}
            $freed+=$item.Length;Remove-Item -LiteralPath $file -Force
            if(Test-Path -LiteralPath $file){throw "Failed cleanup: $file"}
        }
        if((Test-Path -LiteralPath $dir) -and @(Get-ChildItem -LiteralPath $dir -Force).Count -eq 0){Remove-Item -LiteralPath $dir}
    }
    if(!$VerifyOnly -or $freed -gt 0){
        [pscustomobject]@{ReleasedBytes=$freed;MainProjectCleaned=$false;Preserved='Input backup, final catalogues, source hashes and validation logs'} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tideOutput 'catalog-cleanup.json') -Encoding utf8
    }
}
