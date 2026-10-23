param(
    [Parameter(Mandatory = $true)][string]$Before,
    [Parameter(Mandatory = $true)][string]$After
)
$ErrorActionPreference = 'Stop'
$choiceBefore = @(Get-Content -LiteralPath (Join-Path $Before 'events.json') -Raw | ConvertFrom-Json)
$choiceAfter = @(Get-Content -LiteralPath (Join-Path $After 'events.json') -Raw | ConvertFrom-Json)
if ($choiceBefore.Count -ne $choiceAfter.Count) { throw 'Encounter count changed.' }

function Choice-Behavior($row) {
    $eventData = [ordered]@{}
    foreach ($property in $row.Event.PSObject.Properties) {
        if ($property.Name -eq 'Choices') {
            $eventData[$property.Name] = @($property.Value | ForEach-Object {
                $_ | Select-Object * -ExcludeProperty Label, Detail
            })
        } else { $eventData[$property.Name] = $property.Value }
    }
    [pscustomobject][ordered]@{ Origin = $row.Origin; Event = [pscustomobject]$eventData }
}
function Choice-Numbers([string]$value) {
    ([regex]::Matches($value, '\d+(?:\.\d+)?') | ForEach-Object { $_.Value }) -join '|'
}

$choiceChanges = [Collections.Generic.List[object]]::new()
$choiceTexts = [Collections.Generic.List[object]]::new()
$choiceCount = 0
for ($rowIndex = 0; $rowIndex -lt $choiceBefore.Count; $rowIndex++) {
    $old = $choiceBefore[$rowIndex]
    $new = $choiceAfter[$rowIndex]
    $oldBehavior = Choice-Behavior $old | ConvertTo-Json -Depth 20 -Compress
    $newBehavior = Choice-Behavior $new | ConvertTo-Json -Depth 20 -Compress
    if ($oldBehavior -cne $newBehavior) {
        throw "Data outside choice Label/Detail changed at row $rowIndex ($($old.Event.Id))."
    }
    for ($choiceIndex = 0; $choiceIndex -lt $old.Event.Choices.Count; $choiceIndex++) {
        $choiceCount++
        $oldChoice = $old.Event.Choices[$choiceIndex]
        $newChoice = $new.Event.Choices[$choiceIndex]
        $choiceTexts.Add([pscustomobject]@{
            Id = $old.Event.Id; Name = $old.Event.Name; ChoiceIndex = $choiceIndex
            Label = $oldChoice.Label; Detail = $oldChoice.Detail
        })
        if ((Choice-Numbers $oldChoice.Detail) -cne (Choice-Numbers $newChoice.Detail)) {
            throw "Numbers in choice detail changed: $($old.Event.Id) / $choiceIndex."
        }
        if ($oldChoice.Label -ceq $newChoice.Label -and $oldChoice.Detail -ceq $newChoice.Detail) { continue }
        $choiceChanges.Add([pscustomobject][ordered]@{
            Id = $new.Event.Id; Name = $new.Event.Name; ChoiceIndex = $choiceIndex
            BeforeLabel = $oldChoice.Label; AfterLabel = $newChoice.Label
            BeforeDetail = $oldChoice.Detail; AfterDetail = $newChoice.Detail
        })
    }
}
$choiceDistinct = @($choiceTexts | Sort-Object Id, Name, ChoiceIndex, Label, Detail -Unique)
$choiceUniqueChanges = @($choiceChanges | Sort-Object Id, Name, ChoiceIndex, BeforeLabel, BeforeDetail, AfterLabel, AfterDetail -Unique)
$choiceScope = [pscustomobject][ordered]@{
    RuntimeRows = $choiceAfter.Count
    UniqueEventIds = @($choiceAfter.Event.Id | Sort-Object -Unique).Count
    ChoiceRows = $choiceCount
    DistinctChoices = $choiceDistinct.Count
    ChangedChoices = $choiceUniqueChanges.Count
    ChangedLabels = @($choiceUniqueChanges | Where-Object { $_.BeforeLabel -cne $_.AfterLabel }).Count
    ChangedDetails = @($choiceUniqueChanges | Where-Object { $_.BeforeDetail -cne $_.AfterDetail }).Count
    GameplayAndDescriptionsUnchanged = $true
    DetailNumbersUnchanged = $true
}
[pscustomobject][ordered]@{ Scope = $choiceScope; Changes = $choiceUniqueChanges } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $After 'choice-review.json') -Encoding utf8
$choiceScope | ConvertTo-Json -Compress
