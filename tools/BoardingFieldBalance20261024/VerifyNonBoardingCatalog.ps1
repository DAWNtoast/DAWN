param(
    [string]$Before='D:/Game/outputs/boarding-field-balance-20261024/Inputs/Catalog',
    [string]$After='D:/Game/outputs/boarding-field-balance-20261024/VerifiedCatalog',
    [string]$Output=''
)
$ErrorActionPreference='Stop'
if(!$Output){$Output=$After}
New-Item -ItemType Directory -Force -Path $Output | Out-Null
function Get-CatalogRows($directory,$upgraded){
    $taskName=if($upgraded){'card-upgrade-data.json'}else{'card-list-data.json'}
    $taskData=Get-Content -LiteralPath (Join-Path $directory $taskName) -Raw | ConvertFrom-Json
    $taskMap=@{}
    foreach($taskCard in $taskData.Cards){
        $taskVariants=if($upgraded){$taskCard.Stages}else{$taskCard.StageVariants}
        foreach($taskVariant in $taskVariants){
            $taskStages=if($upgraded){@($taskVariant.Stage)}else{@($taskVariant.Stages)}
            foreach($taskStage in $taskStages){
                if($taskStage -eq '接舷'){continue}
                $taskKey=$taskCard.Id+'|'+$taskStage
                if($taskMap.ContainsKey($taskKey)){throw ('Duplicate card stage '+$taskKey+' in '+$taskName)}
                $taskPayload=[ordered]@{Id=$taskCard.Id;Number=$taskCard.Number;SchoolId=$taskCard.SchoolId;School=$taskCard.School;Source=$taskCard.Source;Stage=$taskStage}
                if($upgraded){
                    $taskPayload.Name=$taskCard.Name
                    $taskPayload.UsesSourceExperience=$taskCard.UsesSourceExperience
                    $taskPayload.SharedMaximumLevel=$taskCard.SharedMaximumLevel
                    foreach($taskProperty in $taskVariant.PSObject.Properties){if($taskProperty.Name -ne 'Stage'){$taskPayload[$taskProperty.Name]=$taskProperty.Value}}
                }else{
                    foreach($taskProperty in $taskVariant.PSObject.Properties){if($taskProperty.Name -ne 'Stages'){$taskPayload[$taskProperty.Name]=$taskProperty.Value}}
                }
                $taskMap[$taskKey]=ConvertTo-Json -InputObject $taskPayload -Depth 30 -Compress
            }
        }
    }
    return $taskMap
}
$taskDifferences=[Collections.Generic.List[object]]::new()
$taskCounts=[Collections.Generic.List[object]]::new()
foreach($taskUpgrade in @($false,$true)){
    $taskOld=Get-CatalogRows $Before $taskUpgrade
    $taskNew=Get-CatalogRows $After $taskUpgrade
    $taskKind=if($taskUpgrade){'upgraded'}else{'base'}
    $taskKeys=@(@($taskOld.Keys)+@($taskNew.Keys)|Sort-Object -Unique)
    $taskSame=0
    foreach($taskKey in $taskKeys){
        $taskOldValue=$taskOld[$taskKey];$taskNewValue=$taskNew[$taskKey]
        if($taskOld.ContainsKey($taskKey) -and $taskNew.ContainsKey($taskKey) -and $taskOldValue -ceq $taskNewValue){$taskSame++;continue}
        $taskChange=if(!$taskOld.ContainsKey($taskKey)){'added'}elseif(!$taskNew.ContainsKey($taskKey)){'removed'}else{'changed'}
        $taskDifferences.Add([pscustomobject]@{Catalog=$taskKind;Key=$taskKey;Change=$taskChange;Before=$taskOldValue;After=$taskNewValue})
    }
    $taskCounts.Add([pscustomobject]@{Catalog=$taskKind;BeforeStages=$taskOld.Count;AfterStages=$taskNew.Count;IdenticalStages=$taskSame})
}
@($taskDifferences)|ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $Output 'non-boarding-catalog-differences.json') -Encoding utf8
$taskHashes=foreach($taskDirectory in @($Before,$After)){foreach($taskFile in @('card-list-data.json','card-upgrade-data.json')){[pscustomobject]@{Directory=$taskDirectory;File=$taskFile;SHA256=(Get-FileHash -LiteralPath (Join-Path $taskDirectory $taskFile)).Hash}}}
$taskResult=[ordered]@{VerifiedAtUtc=[DateTime]::UtcNow.ToString('o');Before=$Before;After=$After;Counts=@($taskCounts);Differences=$taskDifferences.Count;NonBoardingUnchanged=($taskDifferences.Count -eq 0);SourceCatalogHashes=@($taskHashes);Scope='Every non-boarding card-stage, base and all upgrade levels: card identity, numbering, school/source, energy and other costs, complete effect/description, keyword definitions, stage upgrade caps and upgrade prices. Grouped stage variants are expanded so a boarding-only split does not produce a false difference.'}
$taskResult|ConvertTo-Json -Depth 30|Set-Content -LiteralPath (Join-Path $Output 'non-boarding-catalog-integrity.json') -Encoding utf8
if($taskDifferences.Count -gt 0){$taskDifferences|Select-Object Catalog,Key,Change|Format-Table;throw ('Unexpected non-boarding catalog changes: '+$taskDifferences.Count)}
$taskCounts|Format-Table
Write-Output 'PASS: every preparation/naval card face and every upgrade level is unchanged.'
