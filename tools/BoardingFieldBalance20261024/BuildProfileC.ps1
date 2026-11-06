$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskExperiments=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024/experiments'
$taskProfile=Join-Path $taskExperiments 'profile-c'
$taskCombined=Join-Path $taskExperiments 'combined-c'
if((Test-Path $taskProfile) -or (Test-Path $taskCombined)){throw 'Candidate C already exists; never overwrite a frozen candidate'}
New-Item -ItemType Directory -Path $taskProfile,$taskCombined | Out-Null
Get-ChildItem (Join-Path $taskExperiments 'profile-b') -Filter '*.cs' | Copy-Item -Destination $taskProfile
function Replace-Once([string]$text,[string]$before,[string]$after){if(($text.Split([string[]]@($before),[StringSplitOptions]::None)).Count -ne 2){throw ('Expected unique source: '+$before)};return $text.Replace($before,$after)}
$taskPath=Join-Path $taskProfile 'CoreLegendaryWarships.cs'
$taskText=[IO.File]::ReadAllText($taskPath)
$taskText=Replace-Once $taskText 'boardingHealthMultiplier: 750m / 504m, boardingGuard: 30' 'boardingHealthMultiplier: 640m / 504m, boardingGuard: 30'
$taskText=Replace-Once $taskText 'boardingHealthMultiplier: 820m / 536m, boardingGuard: 40' 'boardingHealthMultiplier: 720m / 536m, boardingGuard: 40'
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskPath=Join-Path $taskProfile 'CoreRouteBossCatalog.cs'
$taskText=[IO.File]::ReadAllText($taskPath)
$taskText=Replace-Once $taskText 'boardingHealthMultiplier: 220m / 112m, boardingGuard: 15' 'boardingHealthMultiplier: 240m / 112m, boardingGuard: 15'
$taskText=Replace-Once $taskText 'boardingHealthMultiplier: 680m / 240m, boardingGuard: 20' 'boardingHealthMultiplier: 620m / 240m, boardingGuard: 20'
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
$taskPath=Join-Path $taskProfile 'CoreBoardingBossCatalog.cs'
$taskText=[IO.File]::ReadAllText($taskPath)
$taskAnchor='        private static void Add(Dictionary<string, CoreBoardingEnemyDeck> result, string id,'
$taskFunction=@'
        // Boarding attack pressure only: healing, guard, burn and life payments keep their authored budgets.
        private static decimal EncounterAttackPressure(string id)
        {
            switch (id)
            {
                case "legendary_pirate_red_widow": return .75m;
                case "legendary_pirate_thunder_crown": return .85m;
                case "legendary_pirate_tide_requiem": return .80m;
                default: return 1m;
            }
        }

'@
$taskText=Replace-Once $taskText $taskAnchor ($taskFunction+$taskAnchor)
$taskText=Replace-Once $taskText 'decimal guardScale = reference * 2.5m / Math.Max(1, profile.BoardingGuard);' "decimal guardScale = reference * 2.5m / Math.Max(1, profile.BoardingGuard);`r`n            decimal outgoingScale = attackScale * EncounterAttackPressure(id);"
$taskText=Replace-Once $taskText 'CoreBattleBalance.ScaleBattleValue(card.AttackPercent, attackScale)' 'CoreBattleBalance.ScaleBattleValue(card.AttackPercent, outgoingScale)'
$taskText=Replace-Once $taskText 'CoreBattleBalance.ScaleBattleValue(card.BonusPercent, attackScale)' 'CoreBattleBalance.ScaleBattleValue(card.BonusPercent, outgoingScale)'
[IO.File]::WriteAllText($taskPath,$taskText,[Text.UTF8Encoding]::new($false))
Get-ChildItem (Join-Path $taskExperiments 'combined-b') -Filter '*.cs' | Copy-Item -Destination $taskCombined
Get-ChildItem $taskProfile -Filter '*.cs' | Copy-Item -Destination $taskCombined -Force
$taskDifferences=Get-ChildItem $taskCombined -Filter '*.cs' | Where-Object {(Get-FileHash $_.FullName).Hash -ne (Get-FileHash (Join-Path (Join-Path $taskExperiments 'combined-b') $_.Name)).Hash}
if(@($taskDifferences).Count -ne 3){throw ('Expected three changed files: '+($taskDifferences.Name -join ','))}
[pscustomobject]@{Basis='combined-b';ChangedCoreFiles=$taskDifferences.Name;Changes=@('Last horizon boarding HP 750 -> 640','Tide requiem boarding HP 820 -> 720; attack/resource damage x0.80','Red widow attack/resource damage x0.75','Thunder crown attack/resource damage x0.85','Moon arcane boarding HP 680 -> 620','Reef tide boarding HP 220 -> 240');CardsChanged=$false;NavalChanged=$false} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $taskExperiments 'profile-c-manifest.json') -Encoding utf8
Write-Output ('Created isolated C: '+$taskCombined)
