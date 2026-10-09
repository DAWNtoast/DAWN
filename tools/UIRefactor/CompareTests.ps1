[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaselinePath,
    [Parameter(Mandatory = $true)][string]$CurrentPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if (-not [IO.Path]::IsPathRooted($OutputPath)) { throw 'OutputPath must be an absolute file path.' }
$output = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $output) { throw "Refusing to overwrite: $output" }
if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($output)) -PathType Container)) {
    throw 'The explicit output directory must already exist.'
}

function Get-CaseStatus([string]$Result) {
    switch -Regex ($Result) {
        '^(Passed|Success)$' { return 'Passed' }
        '^(Failed|Failure|Error)(\(|$)' { return 'Failed' }
        '^Skipped$' { return 'Skipped' }
        '^Not[- ]?Run(nable)?$' { return 'NotRun' }
        '^Inconclusive$' { return 'Inconclusive' }
        '^Cancel(l)?ed$' { return 'Cancelled' }
        default { return 'Unknown' }
    }
}

function Read-TestResults([string]$Path) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($resolved, $settings)
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    if ($document.DocumentElement.Name -ne 'test-run') {
        throw "Expected a completed NUnit 3 test-run document: $resolved"
    }
    if ([string]::IsNullOrEmpty($document.DocumentElement.GetAttribute('end-time'))) {
        throw "The test run has no end-time; do not compare an unfinished run: $resolved"
    }
    $cases = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $counts = [ordered]@{ Passed = 0; Failed = 0; Skipped = 0; NotRun = 0; Inconclusive = 0; Cancelled = 0; Unknown = 0 }
    foreach ($node in $document.SelectNodes('//test-case')) {
        $name = $node.GetAttribute('fullname')
        if ([string]::IsNullOrEmpty($name)) { throw "Test case missing fullname in $resolved" }
        if ($cases.ContainsKey($name)) { throw "Ambiguous duplicate fullname in ${resolved}: $name" }
        $result = $node.GetAttribute('result')
        $status = Get-CaseStatus $result
        $counts[$status]++
        $messages = @($node.SelectNodes('failure/message | reason/message') | ForEach-Object { $_.InnerText })
        $stackTraces = @($node.SelectNodes('failure/stack-trace') | ForEach-Object { $_.InnerText })
        $cases.Add($name, [pscustomobject][ordered]@{
            name = $name; status = $status; result = $result
            label = $node.GetAttribute('label'); runState = $node.GetAttribute('runstate')
            messages = $messages; stackTraces = $stackTraces
        })
    }
    return [pscustomobject]@{
        cases = $cases
        summary = [pscustomobject][ordered]@{
            path = $resolved; sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
            result = $document.DocumentElement.GetAttribute('result')
            startedAt = $document.DocumentElement.GetAttribute('start-time')
            endedAt = $document.DocumentElement.GetAttribute('end-time')
            discoveredCases = $cases.Count; counts = $counts
        }
    }
}

$baseline = Read-TestResults $BaselinePath
$current = Read-TestResults $CurrentPath
$newFailures = [Collections.Generic.List[object]]::new()
$persistedFailures = [Collections.Generic.List[object]]::new()
$resolvedFailures = [Collections.Generic.List[object]]::new()
$unverifiedFailures = [Collections.Generic.List[object]]::new()
$stateChanges = [Collections.Generic.List[object]]::new()
$addedCases = [Collections.Generic.List[object]]::new()
$missingCases = [Collections.Generic.List[object]]::new()

foreach ($name in ($current.cases.Keys | Sort-Object)) {
    $after = $current.cases[$name]
    $before = if ($baseline.cases.ContainsKey($name)) { $baseline.cases[$name] } else { $null }
    $comparison = [pscustomobject][ordered]@{ name = $name; baseline = $before; current = $after }
    if ($null -eq $before) { $addedCases.Add($after) }
    elseif ($before.result -cne $after.result -or $before.label -cne $after.label -or $before.runState -cne $after.runState) {
        $stateChanges.Add($comparison)
    }
    if ($after.status -eq 'Failed') {
        if ($null -ne $before -and $before.status -eq 'Failed') { $persistedFailures.Add($comparison) }
        else { $newFailures.Add($comparison) }
    }
    elseif ($null -ne $before -and $before.status -eq 'Failed') {
        if ($after.status -eq 'Passed') { $resolvedFailures.Add($comparison) }
        else { $unverifiedFailures.Add($comparison) }
    }
}
foreach ($name in ($baseline.cases.Keys | Sort-Object)) {
    if ($current.cases.ContainsKey($name)) { continue }
    $before = $baseline.cases[$name]
    $missingCases.Add($before)
    if ($before.status -eq 'Failed') {
        $unverifiedFailures.Add([pscustomobject][ordered]@{ name = $name; baseline = $before; current = $null })
    }
}

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    note = 'Resolved means Failed -> Passed only. Skipped, NotRun, inconclusive, cancelled and absent cases do not prove a baseline failure resolved. Raw result, label and runState are retained.'
    baseline = $baseline.summary; current = $current.summary
    summary = [ordered]@{
        newFailures = $newFailures.Count; persistedFailures = $persistedFailures.Count
        resolvedFailures = $resolvedFailures.Count; unverifiedBaselineFailures = $unverifiedFailures.Count
        stateChanges = $stateChanges.Count; addedCases = $addedCases.Count; missingCases = $missingCases.Count
    }
    newFailures = $newFailures.ToArray(); persistedFailures = $persistedFailures.ToArray()
    resolvedFailures = $resolvedFailures.ToArray(); unverifiedBaselineFailures = $unverifiedFailures.ToArray()
    stateChanges = $stateChanges.ToArray(); addedCases = $addedCases.ToArray(); missingCases = $missingCases.ToArray()
}
$json = $report | ConvertTo-Json -Depth 12
# CreateNew also refuses a file created between the initial check and this write.
$stream = [IO.File]::Open($output, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json + [Environment]::NewLine)
    $stream.Write($bytes, 0, $bytes.Length)
} finally { $stream.Dispose() }
[pscustomobject]@{ outputPath = $output; baseline = $baseline.summary.counts; current = $current.summary.counts; comparison = $report.summary }
