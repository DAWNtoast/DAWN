param([Parameter(Mandatory=$true)][string]$RunId)
$ErrorActionPreference='Stop'
if($RunId -notmatch '^[a-f0-9]{32}$'){throw 'Invalid capture run ID.'}
$verifyWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$verifyOutput=Join-Path $verifyWorkspace 'outputs/story-text-fix-20261103'
$verifyCards=Join-Path $verifyWorkspace 'outputs/card-list-by-school-20261023'
$verifyCapture=Join-Path $verifyWorkspace ('outputs/card-game-view-20261030/runs/'+$RunId)
$verifyLatest=Get-Content -LiteralPath (Join-Path $verifyWorkspace 'outputs/card-game-view-20261030/latest-run.json') -Raw | ConvertFrom-Json
if([IO.Path]::GetFullPath($verifyLatest.Evidence) -ne $verifyCapture){throw 'Latest validated capture is a different run.'}
$verifyCleanup=Get-Content -LiteralPath (Join-Path $verifyCapture 'cleanup.json') -Raw | ConvertFrom-Json
if(!$verifyCleanup.Success){throw 'Capture did not pass its final validation.'}
$verifyManifest=Get-Content -LiteralPath (Join-Path $verifyCards 'card-game-view-data.json') -Raw | ConvertFrom-Json
if($verifyManifest.InputFingerprint -ne $verifyLatest.InputFingerprint -or $verifyManifest.RowCount -ne 1017 -or $verifyManifest.Cards.Count -ne 1017){throw 'Invalid final card capture manifest.'}
$verifySourceHashes=Import-Csv -LiteralPath (Join-Path $verifyCapture 'source-hashes.csv')
$verifyResourceHashes=Import-Csv -LiteralPath (Join-Path $verifyCapture 'resource-hashes.csv')
foreach($verifyHash in @($verifySourceHashes)+@($verifyResourceHashes)){
    if((Get-FileHash -LiteralPath $verifyHash.Source -Algorithm SHA256).Hash -ne $verifyHash.SHA256){throw ('Capture source no longer matches: '+$verifyHash.Source)}
}
foreach($verifyImage in $verifyManifest.Cards){
    $verifyImagePath=[IO.Path]::GetFullPath((Join-Path $verifyCards $verifyImage.Image))
    if(!$verifyImagePath.StartsWith($verifyCards+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Image path outside fixed card list.'}
    if((Get-FileHash -LiteralPath $verifyImagePath -Algorithm SHA256).Hash -ne $verifyImage.ImageSha256){throw ('Card image hash differs: '+$verifyImage.Image)}
}
$verifyBefore=Get-Content -LiteralPath (Join-Path $verifyOutput 'input-backup/fixed-card-list/card-list-data.json') -Raw | ConvertFrom-Json
$verifyAfter=Get-Content -LiteralPath (Join-Path $verifyCards 'card-list-data.json') -Raw | ConvertFrom-Json
if(($verifyBefore.Cards | ConvertTo-Json -Depth 30 -Compress) -cne ($verifyAfter.Cards | ConvertTo-Json -Depth 30 -Compress)){throw 'Card numerical definitions changed.'}
$verifyReviewBackup=Join-Path $verifyOutput 'input-backup/fixed-card-list/CardReview.js'
$verifyReviewCurrent=Join-Path $verifyWorkspace 'tools/CardListBySchool20261023/CardReview.js'
if((Get-FileHash -LiteralPath $verifyReviewBackup).Hash -ne (Get-FileHash -LiteralPath $verifyReviewCurrent).Hash){throw 'Review storage script changed.'}
if(!(Get-Content -LiteralPath $verifyReviewCurrent -Raw).Contains('shattered-sail:card-review:v1:')){throw 'Review storage key missing.'}
$verifyOutline=Import-Csv -LiteralPath (Join-Path $verifyWorkspace 'outputs/story-text-audit-20261103/input-hashes.csv') | Where-Object Path -eq (Join-Path $verifyWorkspace 'docs/老船长完整故事大纲.md')
if(!$verifyOutline -or (Get-FileHash -LiteralPath $verifyOutline.Path).Hash -ne $verifyOutline.Hash){throw 'Canonical outline changed.'}
$verifyCsvRelative='UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv'
$verifyCsvBefore=Import-Csv -LiteralPath (Join-Path $verifyOutput ('input-backup/'+$verifyCsvRelative))
$verifyCsvAfter=Import-Csv -LiteralPath (Join-Path $verifyWorkspace $verifyCsvRelative)
if($verifyCsvBefore.Count -ne $verifyCsvAfter.Count){throw 'Component count changed.'}
$verifyDescriptions=0
for($verifyIndex=0;$verifyIndex -lt $verifyCsvBefore.Count;$verifyIndex++){
    foreach($verifyField in $verifyCsvBefore[$verifyIndex].PSObject.Properties.Name){
        if($verifyCsvBefore[$verifyIndex].$verifyField -cne $verifyCsvAfter[$verifyIndex].$verifyField){
            if($verifyField -ne 'description'){throw ('Non-description component field changed: '+$verifyField)}
            $verifyDescriptions++
        }
    }
}
if($verifyDescriptions -ne 7){throw 'Unexpected component description count.'}
[xml]$verifyTests=Get-Content -LiteralPath (Join-Path $verifyOutput 'validation/core-final/results.xml') -Raw
if($verifyTests.DocumentElement.GetAttribute('passed') -ne '102' -or $verifyTests.DocumentElement.GetAttribute('failed') -ne '0'){throw 'Final regression did not pass.'}
foreach($verifyHash in Import-Csv -LiteralPath (Join-Path $verifyOutput 'validation/core-final/source-hashes.csv')){
    if((Get-FileHash -LiteralPath $verifyHash.Source).Hash -ne $verifyHash.SHA256){throw ('Test source changed since final run: '+$verifyHash.Source)}
}
$verifyHtml=Get-Content -LiteralPath (Join-Path $verifyCards '全部卡牌清单.html') -Raw
if(!$verifyHtml.Contains($verifyManifest.Cards[0].Image)){throw 'Fixed HTML missing current game card images.'}
$verifyResult=[pscustomobject]@{
    VerifiedUtc=[DateTime]::UtcNow.ToString('o'); CaptureRun=$RunId; InputFingerprint=$verifyManifest.InputFingerprint
    SourceHashesVerified=$verifySourceHashes.Count; ResourceHashesVerified=$verifyResourceHashes.Count; ActualCardImagesVerified=$verifyManifest.Cards.Count
    CardCount=$verifyAfter.Cards.Count; StageCount=$verifyAfter.StageCount; CardDefinitionsUnchanged=$true
    ReviewStorageScriptUnchanged=$true; OutlineUnchanged=$true; ComponentDescriptionChanges=$verifyDescriptions; OtherComponentFieldsUnchanged=$true
    RegressionPassed=102; RegressionFailed=0; RegressionSkipped=0; FinalTestSourceHashesStable=$true
    FixedHtml=(Join-Path $verifyCards '全部卡牌清单.html'); FixedHtmlSHA256=(Get-FileHash -LiteralPath (Join-Path $verifyCards '全部卡牌清单.html')).Hash
}
$verifyResult | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $verifyOutput 'validation/final-verification.json') -Encoding utf8
$verifyResult | ConvertTo-Json -Depth 4
