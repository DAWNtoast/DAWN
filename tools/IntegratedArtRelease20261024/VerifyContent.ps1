param([ValidatePattern('^[0-9]{2}$')][string]$Revision='01')
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$run=Join-Path $workspace 'Artifacts/IntegratedArtRelease20261024'
$snapshot=Join-Path $run ('Snapshot'+$Revision)
$frozen=Join-Path $run ('FrozenSource'+$Revision+'/UnityClient')
$release=Join-Path $workspace ('Builds/第七港口-美术与界面整合20261025-r'+$Revision)
$player=Join-Path $release 'Windows'
$report=Join-Path $snapshot 'Smoke/Report'
$sourceManifest=Import-Csv -LiteralPath (Join-Path $snapshot 'source-manifest.csv')
$importReport=Get-Content -LiteralPath (Join-Path $snapshot 'Windows/packaged-images.json') -Raw | ConvertFrom-Json
if($importReport.Result -ne 'PASS') { throw 'Build image inclusion verification must pass first.' }
$art=@(Import-Csv -LiteralPath (Join-Path $report 'packaged-integrated-art.csv'))
$components=@(Import-Csv -LiteralPath (Join-Path $report 'packaged-component-tuning.csv'))
if($art.Count -ne 55 -or $components.Count -ne 31) { throw 'Player did not report all 55 art assets and 31 tuned components.' }
$expectedComponents=@(Import-Csv -LiteralPath (Join-Path $workspace 'Artifacts/UnpackagedAudit20261024/component-diff.csv'))
foreach($expected in $expectedComponents) {
 $actual=@($components | Where-Object componentId -eq $expected.Id)
 if($actual.Count -ne 1 -or [decimal]$actual[0].speed -ne [decimal]$expected.CurrentSpeed -or [decimal]$actual[0].vision -ne [decimal]$expected.CurrentVision) { throw ('Packaged component mismatch: '+$expected.Id) }
}
$mapped=Get-Content -LiteralPath (Join-Path $run 'Illustrations/source-mappings.json') -Raw | ConvertFrom-Json
$sourceProof=@(foreach($item in $mapped) {
 $relative=$item.Asset.Substring((Join-Path $workspace 'UnityClient').Length+1)
 $hash=(Get-FileHash -LiteralPath (Join-Path $frozen $relative)).Hash
 if($hash -ne $item.SHA256) { throw ('Frozen illustration differs from approved original: '+$relative) }
 [pscustomobject]@{Resource=$item.Resource;SHA256=$hash;Source=$item.Source}
})
foreach($kind in @('fire','arcane','curse','tide','thunder','blood')) {
 $original=Join-Path $workspace ('outputs/art-redraw-20261024/fields/field_'+$kind+'_v2.png')
 $relative='Assets/_Project/Resources/NavalTactics/Fields/field_'+$kind+'_v2.png'
 $hash=(Get-FileHash -LiteralPath (Join-Path $frozen $relative)).Hash
 if($hash -ne (Get-FileHash -LiteralPath $original).Hash) { throw ('Frozen field differs from original: '+$kind) }
 $sourceProof+=[pscustomobject]@{Resource=('NavalTactics/Fields/field_'+$kind+'_v2');SHA256=$hash;Source=$original}
}
$icon=Get-Content -LiteralPath (Join-Path $workspace 'Artifacts/ArtIntegrationRelease20261024/BattleIcons/integration-manifest.json') -Raw | ConvertFrom-Json
$atlas=(Get-FileHash -LiteralPath (Join-Path $frozen 'Assets/_Project/Resources/BattleIcons/battle_icons_v1.png')).Hash
if($atlas -ne $icon.AtlasSHA256 -or $icon.Result -ne 'PASS' -or $icon.ArtBaseCount -ne 41) { throw 'Frozen icon atlas does not match pixel-verified source art.' }
foreach($source in $icon.Assets) {
 if((Get-FileHash -LiteralPath $source.Source).Hash -ne $source.SourceSHA256) { throw ('Original icon changed: '+$source.Id) }
}
$smoke=Get-Content -LiteralPath (Join-Path $report 'player-smoke-report.txt') -Raw
if($smoke -notmatch '^PASS\r?\n' -or $smoke -notmatch '55 authored art assets' -or $smoke -notmatch '31 updated component') { throw 'Actual player acceptance did not complete.' }
$result=[ordered]@{
 Result='PASS'; Version='0.3.20261025.1'; ArtAssets=$art.Count; ComponentChanges=$components.Count;
 RuntimeArt=$art; RuntimeComponents=$components; DirectArtSourceProof=$sourceProof;
 IconAtlas=@{SHA256=$atlas;Bases=41;Proof='41 source cells verified pixel-identical; runtime graphics select this atlas.'};
 SourceManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $snapshot 'source-manifest.csv')).Hash;
 PackagedImages=$importReport.Images;
 IncludedFixes=@('行动点教程追加移动','冲撞教程要求朝东','教程顶部海面空隙','战斗冗余说明文字','目标港口底栏与定位首领按钮布局','回合开始默认隐藏移动和攻击范围','场地保留图案和右下角数字并轮换海面','状态图标透明底');
 LegacyTreasureCompatibility='在航旧存档沿用旧图和旧字幕；新拼合及新出航地图使用新版三栏漫画。'
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $snapshot 'integrated-content-verification.json') -Encoding utf8
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $player 'integrated-content-manifest.json') -Encoding utf8
$notes=@"
第七港口 0.3.20261025.1
美术与界面整合版

已接入：6页新版宝藏漫画及三栏字幕、41个战斗基础图标、6类透明场地图、四桅飞剪商船档案、新版部件商店背景。
包含31件部件的调参结果，其中25件涉及视野。
包含教程移动/朝东、黑边、小字删除、首领定位布局、默认范围隐藏、场地图案与数字、状态图标透明底等本轮修复。
活动旧宝藏航程保留原有剧情与图片，新出航使用新漫画。
完整自动化回归、实际成品资源/部件加载、ZIP逐文件完整性验证均以随附release-manifest和Artifacts记录为准。
双击 第七港口.exe 启动；保留整个Windows目录，勿只复制EXE。
"@
$notes | Set-Content -LiteralPath (Join-Path $player '版本说明.txt') -Encoding utf8
[pscustomobject]@{Result='PASS';ArtAssets=55;Components=31;PackagedImages=$importReport.Images} | ConvertTo-Json