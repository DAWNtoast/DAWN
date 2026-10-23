param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$Output
)
$ErrorActionPreference = 'Stop'
$auditAssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
$auditNunit = Join-Path ([IO.Path]::GetDirectoryName($auditAssemblyPath)) 'nunit.framework.dll'
if (Test-Path -LiteralPath $auditNunit) { [void][Reflection.Assembly]::LoadFrom($auditNunit) }
$auditAssembly = [Reflection.Assembly]::LoadFrom($auditAssemblyPath)
$auditFlags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
function Audit-Type([string]$name) { $auditAssembly.GetType('ShatteredSail.Core.Gameplay.' + $name, $true) }
function Audit-Call($type, [string]$name, $instance, [object[]]$arguments) {
    $methods = @($type.GetMethods($auditFlags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $arguments.Count })
    if ($methods.Count -ne 1) { throw "Expected one $name factory with $($arguments.Count) arguments; found $($methods.Count)." }
    $methods[0].Invoke($instance, $arguments)
}
$auditRows = [Collections.Generic.List[object]]::new()
function Audit-Add([string]$origin, $events) {
    foreach ($event in $events) {
        $auditRows.Add([pscustomobject][ordered]@{ Origin = $origin; Event = $event })
    }
}
$auditSessionType = Audit-Type 'CoreVoyageSession'
$auditCatalogType = Audit-Type 'CoreVoyageEventCatalog'
$auditRoutes = $auditSessionType.GetProperty('Routes').GetValue($null)
for ($routeIndex = 0; $routeIndex -lt $auditRoutes.Count; $routeIndex++) {
    $route = $auditRoutes[$routeIndex]
    $session = [Activator]::CreateInstance($auditSessionType)
    $progress = [Activator]::CreateInstance((Audit-Type 'CoreTreasureProgress'))
    $progress.pirateExperience = 1500
    [void](Audit-Call $auditSessionType 'RestoreTreasureProgress' $session @($progress))
    [void](Audit-Call $auditSessionType 'SelectRoute' $session @([int]$routeIndex))
    [void](Audit-Call $auditSessionType 'SetSeed' $session @('EVENT-TEXT-AUDIT'))
    [void](Audit-Call $auditSessionType 'StartVoyage' $session @())
    $baseEvents = @(Audit-Call $auditSessionType 'CreateEncounterPool' $session @($route))
    Audit-Add ($route.Id + '/base') $baseEvents
    foreach ($event in $baseEvents) {
        Audit-Add ($route.Id + '/exploration') (Audit-Call $auditCatalogType 'EnrichExploration' $null @($event, $route.Id))
    }
    Audit-Add ($route.Id + '/fixed') (Audit-Call $auditCatalogType 'CreateExpandedFixedEncounters' $null @($route.Id))
    foreach ($id in (Audit-Call $auditCatalogType 'RandomEventIds' $null @($route.Id))) {
        Audit-Add ($route.Id + '/random') (Audit-Call $auditCatalogType 'CreateRandomEncounter' $null @($id, $route.RecommendedPower))
    }
    Audit-Add ($route.Id + '/legacy-boss') (Audit-Call $auditSessionType 'CreateBossEncounter' $session @($route))
    foreach ($id in @('random_tailwind', 'random_floating_supplies', 'random_food_spoilage')) {
        Audit-Add ($route.Id + '/legacy-random') (Audit-Call $auditSessionType 'CreateSeaChartRandomEncounter' $session @($id))
    }
    foreach ($detected in @($false, $true)) {
        Audit-Add ($route.Id + '/ambush') (Audit-Call $auditSessionType 'CreateRandomAmbush' $session @($detected))
    }
}
foreach ($boss in (Audit-Type 'CoreRouteBossCatalog').GetProperty('All').GetValue($null)) {
    Audit-Add 'route-boss' $boss.CreateEncounter()
}
foreach ($ship in (Audit-Type 'CoreLegendaryWarships').GetProperty('All').GetValue($null)) {
    Audit-Add 'legendary' $ship.CreateEncounter()
}
Audit-Add 'treasure-boss' (Audit-Call (Audit-Type 'CoreLegendaryWarships') 'CreateTreasureEncounter' $null @())
foreach ($factory in @('FirstEvent', 'OptionalEvent', 'CrewRequirementEvent', 'NavigatorEvent', 'AmbushEvent', 'BossEvent')) {
    Audit-Add 'guided' (Audit-Call (Audit-Type 'CoreGuidedTutorialContent') $factory $null @())
}
foreach ($lucky in @($false, $true)) {
    Audit-Add ('guided/random/' + $lucky) (Audit-Call (Audit-Type 'CoreGuidedTutorialContent') 'RandomEvent' $null @($lucky))
}
$auditTutorialType = $auditAssembly.GetType('ShatteredSail.Core.Tutorial.TutorialVoyageSession', $true)
foreach ($field in @('firstLegEncounters', 'advancedLegEncounters')) {
    Audit-Add ('legacy-tutorial/' + $field) $auditTutorialType.GetField($field, $auditFlags).GetValue($null)
}
foreach ($rank in (Audit-Type 'CoreTreasureHuntCatalog').GetProperty('Ranks').GetValue($null)) {
    Audit-Add ('legacy-treasure/' + $rank.Id) (Audit-Call (Audit-Type 'CoreTreasureHuntCatalog') 'CreateEncounters' $null @($rank))
    Audit-Add ('treasure-expedition/' + $rank.Id) (Audit-Call (Audit-Type 'CoreTreasureExpeditionCatalog') 'CreateEncounters' $null @($rank))
}
$auditOutput = [IO.Path]::GetFullPath($Output)
[void][IO.Directory]::CreateDirectory($auditOutput)
$auditRows | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $auditOutput 'events.json') -Encoding utf8
$auditRows | ForEach-Object {
    [pscustomobject][ordered]@{ Origin = $_.Origin; Event = $_.Event | Select-Object * -ExcludeProperty Description }
} | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $auditOutput 'gameplay.json') -Encoding utf8
$auditDescriptions = @($auditRows | ForEach-Object {
    [pscustomobject][ordered]@{ Id = $_.Event.Id; Name = $_.Event.Name; Description = $_.Event.Description }
} | Sort-Object Id, Description -Unique)
$auditDescriptions | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $auditOutput 'descriptions.json') -Encoding utf8
$auditSummary = [pscustomobject][ordered]@{
    RuntimeRows = $auditRows.Count
    UniqueIds = @($auditRows.Event.Id | Sort-Object -Unique).Count
    DistinctDescriptions = $auditDescriptions.Count
    ChoiceRows = ($auditRows | ForEach-Object { $_.Event.Choices.Count } | Measure-Object -Sum).Sum
    AssemblySHA256 = (Get-FileHash -LiteralPath $auditAssemblyPath -Algorithm SHA256).Hash
}
$auditSummary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditOutput 'summary.json') -Encoding utf8
$auditSummary | ConvertTo-Json -Compress
