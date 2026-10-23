param(
    [Parameter(Mandatory = $true)][string]$Before,
    [Parameter(Mandatory = $true)][string]$After
)
$ErrorActionPreference = 'Stop'
$auditBeforeRows = @(Get-Content -LiteralPath (Join-Path $Before 'events.json') -Raw | ConvertFrom-Json)
$auditAfterRows = @(Get-Content -LiteralPath (Join-Path $After 'events.json') -Raw | ConvertFrom-Json)
$auditBeforeHash = (Get-FileHash -LiteralPath (Join-Path $Before 'gameplay.json') -Algorithm SHA256).Hash
$auditAfterHash = (Get-FileHash -LiteralPath (Join-Path $After 'gameplay.json') -Algorithm SHA256).Hash
if ($auditBeforeRows.Count -ne $auditAfterRows.Count -or $auditBeforeHash -ne $auditAfterHash) {
    throw 'Encounter data outside Description changed. Review gameplay.json before accepting the audit.'
}
$auditChanges = @(for ($i = 0; $i -lt $auditBeforeRows.Count; $i++) {
    $old = $auditBeforeRows[$i]
    $new = $auditAfterRows[$i]
    if ($old.Event.Description -ceq $new.Event.Description) { continue }
    [pscustomobject][ordered]@{
        Id = $new.Event.Id
        Name = $new.Event.Name
        Before = $old.Event.Description
        After = $new.Event.Description
    }
})
$auditChanges = @($auditChanges | Sort-Object Id, Before, After -Unique)
$auditScope = Get-Content -LiteralPath (Join-Path $After 'summary.json') -Raw | ConvertFrom-Json
$auditResult = [pscustomobject][ordered]@{
    Scope = $auditScope
    GameplayUnchanged = $true
    ChangedEventIds = @($auditChanges.Id | Sort-Object -Unique).Count
    ChangedDescriptions = $auditChanges.Count
    Changes = $auditChanges
}
$auditResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $After 'review.json') -Encoding utf8
[pscustomobject]@{
    RuntimeRows = $auditScope.RuntimeRows
    UniqueIds = $auditScope.UniqueIds
    DistinctDescriptions = $auditScope.DistinctDescriptions
    GameplayUnchanged = $true
    ChangedEventIds = $auditResult.ChangedEventIds
    ChangedDescriptions = $auditResult.ChangedDescriptions
} | ConvertTo-Json -Compress
