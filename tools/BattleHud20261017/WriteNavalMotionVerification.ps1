param(
    [string]$EditResults = (Join-Path $PSScriptRoot '../../Artifacts/NavalFieldStatusMotion20261017/Edit04/edit-results.xml'),
    [string]$PlayResults = (Join-Path $PSScriptRoot '../../Artifacts/NavalFieldStatusMotion20261017/Play03/play-results.xml'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../Artifacts/NavalFieldStatusMotion20261017')
)

# Evidence only: never synchronizes sources, invokes Unity, or touches previews/player storage.
$ErrorActionPreference = 'Stop'
$navalWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$navalArtifacts = Join-Path $navalWorkspace 'Artifacts/NavalFieldStatusMotion20261017'
$navalSourceRoot = Join-Path $navalWorkspace 'UnityClient/Assets/_Project'
$navalIsolatedRoot = Join-Path $navalWorkspace 'tmp/shipyard-detail-clean-20261011/Assets/_Project'

function Assert-PlainWorkspacePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (-not $full.StartsWith($navalWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path must remain inside this workspace: $full"
    }
    # Reject junctions/symlinks so the fixed validation roots cannot redirect reads or output.
    $cursor = $full
    while ($cursor -and -not [string]::Equals($cursor, $navalWorkspace, [StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Reparse points are not allowed in verification paths: $cursor"
            }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $full
}

function Resolve-TaskArtifactPath([string]$Path) {
    $full = Assert-PlainWorkspacePath $Path
    $root = [IO.Path]::GetFullPath($navalArtifacts).TrimEnd('\', '/')
    if (-not [string]::Equals($full, $root, [StringComparison]::OrdinalIgnoreCase) -and
        -not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Test results and reports must remain under $root"
    }
    return $full
}

function Read-TestEvidence([string]$Path) {
    $evidence = [ordered]@{
        path = $Path; exists = $false; validXml = $false; success = $false
        result = $null; total = $null; passed = $null; failed = $null
        skipped = $null; inconclusive = $null; duration = $null
        failedTests = @(); error = $null
    }
    $reader = $null
    try {
        $evidence.path = Resolve-TaskArtifactPath $Path
        $evidence.exists = Test-Path -LiteralPath $evidence.path -PathType Leaf
        if (-not $evidence.exists) { throw 'Test result XML is missing.' }
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create($evidence.path, $settings)
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        $run = $document.SelectSingleNode('/test-run')
        if ($null -eq $run) { throw 'Expected a NUnit test-run document.' }
        foreach ($attribute in @('total', 'passed', 'failed', 'skipped', 'inconclusive')) {
            [long]$count = 0
            if (-not [long]::TryParse($run.GetAttribute($attribute), [ref]$count) -or $count -lt 0) {
                throw "Missing or invalid test count: $attribute"
            }
            $evidence[$attribute] = $count
        }
        $evidence.result = $run.GetAttribute('result')
        $evidence.duration = $run.GetAttribute('duration')
        $evidence.failedTests = @($document.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
            $message = $_.SelectSingleNode('failure/message')
            [ordered]@{
                name = $_.GetAttribute('fullname'); result = $_.GetAttribute('result')
                label = $_.GetAttribute('label'); message = if ($message) { $message.InnerText } else { $null }
            }
        })
        $evidence.validXml = $true
        $evidence.success = $evidence.result -eq 'Passed' -and $evidence.total -gt 0 -and
            $evidence.passed -gt 0 -and $evidence.failed -eq 0 -and $evidence.failedTests.Count -eq 0
        if (-not $evidence.success) { $evidence.error = 'The test run did not finish with a passing result.' }
    }
    catch { $evidence.error = $_.Exception.Message }
    finally { if ($reader) { $reader.Dispose() } }
    return $evidence
}

$navalFiles = @(
    'Core/Runtime/CoreNavalFields.cs'
    'Core/Runtime/CoreNavalFieldsExecution.cs'
    'Presentation/Runtime/NavalFieldBoardOverlay.cs'
    'Presentation/Runtime/NavalFieldGraphic.cs'
    'Presentation/Runtime/NavalFieldMotion.cs'
    'Presentation/Runtime/NavalTacticalBoardView.cs'
    'Presentation/Runtime/BattleStatusRow.cs'
    'Presentation/Runtime/BattleHudChangeMotion.cs'
    'Presentation/Runtime/BattleResourceChangeTracker.cs'
    'Presentation/Runtime/CoreGameplayBattleStatusHud.cs'
    'Presentation/Runtime/CoreGameplayCompactBattleHud.cs'
    'Presentation/Runtime/CoreGameplayBattleAnimationController.cs'
    'Presentation/Runtime/CoreGameplayNavalBattlePresentation.cs'
    'Presentation/Runtime/CoreGameplayNavalTacticalScene.cs'
    'Tests/EditMode/BattleHudStateChangeTests.cs'
    'Tests/EditMode/NavalFieldMotionTests.cs'
    'Tests/PlayMode/NavalFieldStatusMotionTests.cs'
    'Tests/PlayMode/BattleCardResponsivenessTests.cs'
    'Tests/PlayMode/BattleRetainedTurnTests.cs'
)

try {
    $navalOutput = Resolve-TaskArtifactPath $OutputDirectory
    $navalReportPath = Assert-PlainWorkspacePath (Join-Path $navalOutput 'verification.json')
    $navalEdit = Read-TestEvidence $EditResults
    $navalPlay = Read-TestEvidence $PlayResults
    $navalHashes = @($navalFiles | ForEach-Object {
        $entry = [ordered]@{
            path = $_; source = (Join-Path $navalSourceRoot $_); isolated = (Join-Path $navalIsolatedRoot $_)
            sourceSha256 = $null; isolatedSha256 = $null; matches = $false; error = $null
        }
        try {
            $entry.source = Assert-PlainWorkspacePath $entry.source
            $entry.isolated = Assert-PlainWorkspacePath $entry.isolated
            if (-not (Test-Path -LiteralPath $entry.source -PathType Leaf) -or
                -not (Test-Path -LiteralPath $entry.isolated -PathType Leaf)) { throw 'A source or isolated-project file is missing.' }
            $entry.sourceSha256 = (Get-FileHash -LiteralPath $entry.source -Algorithm SHA256).Hash
            $entry.isolatedSha256 = (Get-FileHash -LiteralPath $entry.isolated -Algorithm SHA256).Hash
            $entry.matches = $entry.sourceSha256 -eq $entry.isolatedSha256
            if (-not $entry.matches) { $entry.error = 'Source and isolated-project SHA256 hashes differ.' }
        }
        catch { $entry.error = $_.Exception.Message }
        $entry
    })
    $navalMismatches = @($navalHashes | Where-Object { -not $_.matches })
    $navalErrors = @(
        if (-not $navalEdit.success) { 'Edit: ' + $navalEdit.error }
        if (-not $navalPlay.success) { 'Play: ' + $navalPlay.error }
        foreach ($entry in $navalMismatches) { $entry.path + ': ' + $entry.error }
    )
    $navalReport = [ordered]@{
        version = 1; generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        success = $navalErrors.Count -eq 0
        sourceRoot = $navalSourceRoot; isolatedRoot = $navalIsolatedRoot
        testResults = [ordered]@{ edit = $navalEdit; play = $navalPlay }
        sources = [ordered]@{
            checkedCount = $navalHashes.Count; matchingCount = $navalHashes.Count - $navalMismatches.Count
            mismatchingCount = $navalMismatches.Count; files = $navalHashes
        }
        errors = $navalErrors
    }
    [IO.Directory]::CreateDirectory($navalOutput) | Out-Null
    [IO.File]::WriteAllText($navalReportPath, ($navalReport | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    Write-Output ("Verification {0}: Edit {1}/{2}; Play {3}/{4}; matching sources {5}/{6}." -f
        $navalReport.success, $navalEdit.passed, $navalEdit.total, $navalPlay.passed, $navalPlay.total,
        $navalReport.sources.matchingCount, $navalReport.sources.checkedCount)
    Write-Output $navalReportPath
    if (-not $navalReport.success) { exit 1 }
    exit 0
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
