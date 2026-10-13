$ErrorActionPreference='Stop'
$inventoryWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$inventoryOutput=Join-Path $inventoryWorkspace 'Artifacts/CardSchools20261011/LegacyArtRedraw'
$inventoryBuild=Join-Path $inventoryWorkspace 'tmp/LegacyArtInventory20261012'
$inventoryUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Path $inventoryBuild,$inventoryOutput -Force | Out-Null
$inventoryExe=Join-Path $inventoryBuild 'LegacyCardInventory.exe'
$compileArgs=@((Join-Path $inventoryUnity 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$inventoryExe")
foreach($lib in @('mscorlib.dll','System.dll','System.Core.dll','System.Web.Extensions.dll')) {
 $compileArgs+='-r:'+(Join-Path $inventoryUnity ('MonoBleedingEdge/lib/mono/4.7.2-api/'+$lib))
}
$compileArgs+=@(Get-ChildItem -LiteralPath (Join-Path $inventoryWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$compileArgs+=Join-Path $inventoryWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs'
$compileArgs+=Join-Path $PSScriptRoot 'LegacyCardInventory.cs'
& (Join-Path $inventoryUnity 'NetCoreRuntime/dotnet.exe') @compileArgs | Out-Host
if($LASTEXITCODE -ne 0){throw 'Legacy inventory production compile failed.'}
& (Join-Path $inventoryUnity 'MonoBleedingEdge/bin/mono.exe') $inventoryExe (Join-Path $inventoryOutput 'legacy-card-runtime-raw.json') | Out-Host
if($LASTEXITCODE -ne 0){throw 'Legacy inventory runtime count assertion failed.'}
$inventory=Get-Content -LiteralPath (Join-Path $inventoryOutput 'legacy-card-runtime-raw.json') -Raw | ConvertFrom-Json
$resources=Join-Path $inventoryWorkspace 'UnityClient/Assets/_Project/Resources'
$componentRows=@{}
foreach($component in (Import-Csv -LiteralPath (Join-Path $resources 'Data/component_catalog_v2.csv'))) { $componentRows[$component.component_id]=$component }
$extraArt=@{
    bow_sunwake_crest='bow_gilded_mermaid'; sail_evening_silk='sail_guild_silk';
    cannon_tradeguard_battery='cannon_starfire_battery'; plating_trade_oathwall='plating_gilded_bulwark';
    instrument_tradeledger_oracle='instrument_guild_engine'
}
$portSlugs=@{'断锚港'='broken_anchor';'白贝港'='white_shell';'铜钟港'='bronze_bell';'盐冠港'='salt_crown';'暮潮港'='twilight_tide';'灰鸥港'='gray_gull';'灰鸥湾'='gray_gull'}
$professionNames=@{navigator='领航员';carpenter='木工';doctor='医生';gunner='炮手';cook='厨师';diver='潜水员'}
$slotNames=@{bow='船首';sail='船帆';cannon='火炮';plating='装甲';instrument='仪器'}
$phases=@{Preparation='备战';Naval='炮战';Boarding='接舷'}
$cells=@{
 card_chain_shot=@(1,0);card_full_sail_turn=@(3,0);card_evasive_maneuver=@(0,1);card_boarding_hook=@(1,1);
 card_cutlass_raid=@(2,1);card_emergency_repair=@(3,1);card_cannon_barrage=@(0,0);card_brace=@(0,2)
}
$left=@(23,329,634,939);$right=@(313,619,923,1230);$top=@(25,329,634,937);$bottom=@(313,618,921,1226)
$positions=@('左上','中上','右上','左下','中下','右下')
foreach($card in $inventory.cards) {
    $cell=$cells[$card.oldArtId]
    if($null -eq $cell){throw ('Unmapped old atlas ArtId '+$card.oldArtId)}
    $card | Add-Member existingCardArt ([pscustomobject]@{
      resourceKey='LegacyDemo/card_icon_sheet';path=(Join-Path $resources 'LegacyDemo/card_icon_sheet.png');
      exists=(Test-Path -LiteralPath (Join-Path $resources 'LegacyDemo/card_icon_sheet.png'));
      gridColumns=4;gridRows=4;column=$cell[0];rowFromTop=$cell[1];
      pixelBounds=[pscustomobject]@{left=$left[$cell[0]];right=$right[$cell[0]];top=$top[$cell[1]];bottom=$bottom[$cell[1]]}
    })
    $card | Add-Member targetResourceKey ('CardArt/Western/'+$card.cardId)
    $card | Add-Member targetPngPath (Join-Path $resources ('CardArt/Western/'+$card.cardId+'.png'))
    $card | Add-Member stagesChinese @($card.phase | ForEach-Object {$phases[$_]})
    $card | Add-Member portraitReference $null
    $card | Add-Member componentReference $null
    if($null -ne $card.crewIdentity) {
      $identity=$card.crewIdentity
      $slug=$portSlugs[$identity.port]
      if(-not $slug){throw ('Unmapped crew port '+$identity.port)}
      $portrait=Join-Path $resources ('PortArt/CrewRegional/crew_'+$slug+'_portraits_v2.png')
      $column=[int]$identity.professionIndex%3;$row=[math]::Floor([int]$identity.professionIndex/3)
      $card.portraitReference=[pscustomobject]@{
        path=$portrait;exists=(Test-Path -LiteralPath $portrait);resourceKey=('PortArt/CrewRegional/crew_'+$slug+'_portraits_v2');
        gridColumns=3;gridRows=2;column=$column;rowFromTop=[int]$row;panelPosition=$positions[$identity.professionIndex];
        normalizedUv=[pscustomobject]@{x=$column/3.0;y=1-($row+1)/2.0;width=1/3.0;height=0.5};
        headOnlyUv=[pscustomobject]@{x=$column/3.0+0.15/3.0;y=1-($row+1)/2.0+0.30/2.0;width=0.70/3.0;height=0.65/2.0};
        dossierPanel='Tavern Crew Detail Portrait Frame';dossierPanelSize='390×422';dossierPanelPosition='(-580,-22)';
        displayedImage='Tavern Crew Detail Full Body';displayedImageSize='340×340';
        identityGuidance='保留来源角色身份、脸型、年龄、肤色与发型；服装、装备与场景按西方中世纪航海画风重绘。'
      }
    } elseif($card.sourceCategory -eq 'genericProfessionCompatibility') {
      $card.sourceName='旧通用'+$professionNames[$card.sourceId]+'兼容牌（无独立角色身份）'
    } elseif($card.sourceCategory -eq 'genericComponentCompatibility') {
      $card.sourceName='旧通用'+$slotNames[$card.sourceId]+'兼容牌（无独立部件身份）'
    } elseif($card.sourceKind -eq 'component') {
      $component=$componentRows[$card.sourceId]
      $artSource=if($null -ne $component){$component.art_source_id}else{$extraArt[$card.sourceId]}
      if(-not $artSource){throw ('Missing component art reference '+$card.sourceId)}
      $slot=$card.sourceId.Split('_')[0]
      $preview=Join-Path $resources ('ComponentArt/'+$artSource+'.png')
      $layer=Join-Path $resources ('ModularShip/Layers/'+$artSource+'.png')
      $card.componentReference=[pscustomobject]@{
        id=$card.sourceId;name=$card.sourceName;slot=$slot;slotChinese=$slotNames[$slot];rarity='Rare';rarityChinese='蓝色';
        artSourceId=$artSource;previewPath=$preview;previewExists=(Test-Path -LiteralPath $preview);
        shipLayerPath=$layer;shipLayerExists=(Test-Path -LiteralPath $layer);
        sourceDescription=if($component){$component.description}else{$card.sourceLore};
        sourceEquipmentEffect=if($component){$component.effect}else{''}
      }
    }
}
$counts=$inventory.counts
$counts | Add-Member uniqueRedrawCardIds @($inventory.cards.cardId | Sort-Object -Unique).Count
$counts | Add-Member uniqueOldArtIds @($inventory.cards.oldArtId | Sort-Object -Unique).Count
$counts | Add-Member identityCrewCards @($inventory.cards | Where-Object sourceKind -eq 'crew').Count
$counts | Add-Member identityComponentCards @($inventory.cards | Where-Object sourceKind -eq 'component').Count
$counts | Add-Member sourceFreeBaseCards @($inventory.cards | Where-Object sourceCategory -eq 'sourceFreeBase').Count
$counts | Add-Member genericProfessionCompatibilityCards @($inventory.cards | Where-Object sourceCategory -eq 'genericProfessionCompatibility').Count
$counts | Add-Member genericComponentCompatibilityCards @($inventory.cards | Where-Object sourceCategory -eq 'genericComponentCompatibility').Count
$groups=@($inventory.cards | Group-Object oldArtId | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{oldArtId=$_.Name;cardCount=$_.Count;cardIds=@($_.Group.cardId);existingCardArt=$_.Group[0].existingCardArt}
})
$inventory | Add-Member sharedOldArtGroups $groups
$inventory | Add-Member notes @(
 '重绘键是每张有效旧牌的cardId；旧artId只记录来源，不用于合并新插画。80张旧牌需80张独立PNG。',
 '排除132张六体系牌、4张新普通厨师专属牌和3个AllowedStages.None保留槽；不改卡牌数值、ID、索引或旧ArtId。',
 'description/effect为当前0阶定义的完整中文文案。displayDescriptionByStage与portDisplayDescription来自真实公开session API，展示上下文另列；接舷人数、药品、强化和战斗增益可能改变实际值。',
 '19张无来源基础牌及11张旧通用兼容牌均计入有效CardLibrary；通用职业/槽位牌不是额外的独立角色或具体部件身份。',
 '20张有身份旧角色牌+4张已绘新普通厨师牌+12张体系角色牌构成36位船员一人一卡；30张蓝色部件专属均属普通基础类型。',
 '头像参照是现有3列2行港口人物图集，rowFromTop从上往下；panelPosition给出角色格位。已有图仅作身份参照，重绘仍遵循西方中世纪航海画风。',
 '旧atlas保留缺图fallback；完整交付要求216张有效牌分别有独立西方卡图。'
)
$jsonPath=Join-Path $inventoryOutput 'legacy-card-inventory.json'
$inventory | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $jsonPath -Encoding utf8
$text=[Collections.Generic.List[string]]::new()
$text.Add('旧卡独立插画重绘清单（按CardId）')
$text.Add(('运行时219槽；有效216张；排除132体系+4新中立+3保留槽；旧卡80张，共享旧ArtId仅8个。'))
$text.Add(('旧卡构成：19无来源基础，6通用职业兼容，5通用槽位兼容，20具名角色，30蓝色部件。'))
$text.Add('数值与ID不变；每张旧牌独立重绘，旧ArtId仅为溯源。')
foreach($card in $inventory.cards) {
 $text.Add('');$text.Add(('['+$card.index+'] '+$card.name+' | '+$card.cardId))
 $text.Add(('来源：'+$card.sourceKind+' / '+$card.sourceCategory+' / '+$card.sourceId+' / '+$card.sourceName))
 $text.Add(('阶段：'+($card.stagesChinese -join '、')+'；行动点 '+$card.energy+'；火药 '+$card.powderCost+'；炮弹 '+$card.cannonballCost))
 $text.Add(('0阶定义：'+$card.description))
 foreach($stage in $card.phase){$text.Add(('当前'+$phases[$stage]+'展示：'+$card.displayDescriptionByStage.$stage))}
 $text.Add(('港口展示：'+$card.portDisplayDescription))
 $text.Add(('旧图：'+$card.oldArtId+'；图集 '+$card.existingCardArt.path+'；第'+($card.existingCardArt.rowFromTop+1)+'行第'+($card.existingCardArt.column+1)+'列'))
 if($card.portraitReference){$text.Add(('角色：'+$card.crewIdentity.appearanceTags));$text.Add(('头像参照：'+$card.portraitReference.path+'；'+$card.portraitReference.panelPosition));$text.Add(('背景：'+$card.crewIdentity.biography))}
 if($card.componentReference){$text.Add(('部件参照：'+$card.componentReference.previewPath+'；'+$card.componentReference.slotChinese+'／蓝色'))}
 if($card.sourceLore){$text.Add(('来源故事：'+$card.sourceLore))}
 $text.Add(('独立新图：'+$card.targetPngPath))
}
$text | Set-Content -LiteralPath (Join-Path $inventoryOutput 'legacy-card-inventory.txt') -Encoding utf8
Write-Output $jsonPath
$counts | Format-List
