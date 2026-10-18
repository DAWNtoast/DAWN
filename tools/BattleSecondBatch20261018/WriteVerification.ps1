param(
    [string]$EditResults = '',
    [string]$PlayResults = '',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../Artifacts/BattleSecondBatch20261018'),
    [string]$ValidationProjectPath = (Join-Path $PSScriptRoot '../../tmp/shipyard-detail-clean-20261011')
)

# Evidence only: read source/isolated files and XML; write this task's verification.json.
# Never synchronizes, starts Unity, builds, deletes, or visits preview/player-storage directories.
$ErrorActionPreference = 'Stop'
$batchWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$batchArtifacts = Join-Path $batchWorkspace 'Artifacts/BattleSecondBatch20261018'
$batchSourceRoot = Join-Path $batchWorkspace 'UnityClient/Assets/_Project'

function Assert-PlainWorkspacePath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'A path was not supplied.' }
    if (-not [IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $batchWorkspace $Path }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    if (-not $full.StartsWith($batchWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path must remain inside this workspace: $full"
    }
    $cursor = $full
    while ($cursor -and -not [string]::Equals($cursor, $batchWorkspace, [StringComparison]::OrdinalIgnoreCase)) {
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
    $taskRoot = [IO.Path]::GetFullPath($batchArtifacts).TrimEnd('\', '/')
    if (-not [string]::Equals($full, $taskRoot, [StringComparison]::OrdinalIgnoreCase) -and
        -not $full.StartsWith($taskRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Test results and reports must remain under $taskRoot"
    }
    return $full
}

function Read-TestEvidence([string]$Path) {
    $evidence = [ordered]@{
        path = $Path; exists = $false; validXml = $false; success = $false
        result = $null; total = $null; passed = $null; failed = $null
        skipped = $null; inconclusive = $null; duration = $null
        startedAtUtc = $null; endedAtUtc = $null; failedTests = @(); error = $null
    }
    $reader = $null
    try {
        if ([string]::IsNullOrWhiteSpace($Path)) { throw 'No final test result XML was supplied.' }
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
        $evidence.startedAtUtc = $run.GetAttribute('start-time')
        $evidence.endedAtUtc = $run.GetAttribute('end-time')
        $evidence.failedTests = @($document.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
            $message = $_.SelectSingleNode('failure/message')
            [ordered]@{
                name = $_.GetAttribute('fullname'); result = $_.GetAttribute('result')
                label = $_.GetAttribute('label'); message = if ($message) { $message.InnerText } else { $null }
            }
        })
        $evidence.validXml = $true
        $evidence.success = $evidence.result -eq 'Passed' -and $evidence.total -gt 0 -and
            $evidence.passed -eq $evidence.total -and $evidence.failed -eq 0 -and
            $evidence.skipped -eq 0 -and $evidence.inconclusive -eq 0 -and $evidence.failedTests.Count -eq 0
        if (-not $evidence.success) { $evidence.error = 'The selected test run did not finish with every test passing.' }
    }
    catch { $evidence.error = $_.Exception.Message }
    finally { if ($reader) { $reader.Dispose() } }
    return $evidence
}

# Delivery sources only. The parent task must confirm this final list before recording acceptance.
# Paths are relative to Assets/_Project in both the source and isolated validation project.
$batchFiles = @(
    'Core/Runtime/CoreBoardingCardMechanics.cs'
    'Core/Runtime/CoreBoardingBattleExecution.cs'
    'Core/Runtime/CoreBoardingResolvedAttacks.cs'
    'Core/Runtime/CoreNavalActionMovement.cs'
    'Presentation/Runtime/BattleSchoolAttackGraphic.cs'
    'Presentation/Runtime/CoreGameplayBattleSchoolAttackPresentation.cs'
    'Presentation/Runtime/CoreGameplayBoardingEnemyPresentation.cs'
    'Presentation/Runtime/BattleResolutionGraphic.cs'
    'Presentation/Runtime/BattleResolutionMotion.cs'
    'Presentation/Runtime/CoreGameplayBattleResolutionPresentation.cs'
    'Presentation/Runtime/CoreGameplayBattleAnimationController.cs'
    'Presentation/Runtime/CoreGameplayLifecycle.cs'
    'Presentation/Runtime/CoreGameplayNavalTacticalScene.cs'
    'Tests/EditMode/BattleSchoolAttackGraphicTests.cs'
    'Tests/EditMode/CoreBoardingResolvedAttackTests.cs'
    'Tests/EditMode/CoreNavalFieldsTests.cs'
    'Tests/EditMode/BattleResolutionMotionTests.cs'
    'Tests/PlayMode/BattleSecondBatchMotionTests.cs'
)

try {
    $batchOutput = Resolve-TaskArtifactPath $OutputDirectory
    $batchReportPath = Assert-PlainWorkspacePath (Join-Path $batchOutput 'verification.json')
    $batchIsolatedProject = Assert-PlainWorkspacePath $ValidationProjectPath
    $batchTempRoot = [IO.Path]::GetFullPath((Join-Path $batchWorkspace 'tmp')).TrimEnd('\', '/')
    if (-not $batchIsolatedProject.StartsWith($batchTempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Validation project must be an isolated directory under $batchTempRoot"
    }
    $batchIsolatedRoot = Join-Path $batchIsolatedProject 'Assets/_Project'
    $batchEdit = Read-TestEvidence $EditResults
    $batchPlay = Read-TestEvidence $PlayResults
    $batchHashes = @($batchFiles | ForEach-Object {
        $entry = [ordered]@{
            path = $_; source = (Join-Path $batchSourceRoot $_); isolated = (Join-Path $batchIsolatedRoot $_)
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
    $batchMismatches = @($batchHashes | Where-Object { -not $_.matches })
    $batchErrors = @(
        if (-not $batchEdit.success) { 'Edit: ' + $batchEdit.error }
        if (-not $batchPlay.success) { 'Play: ' + $batchPlay.error }
        foreach ($entry in $batchMismatches) { $entry.path + ': ' + $entry.error }
    )
    $batchReport = [ordered]@{
        version = 1; generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        success = $batchErrors.Count -eq 0
        scope = 'Second-batch battle school attacks and battle resolution presentation'
        sourceRoot = $batchSourceRoot; isolatedRoot = $batchIsolatedRoot
        testResults = [ordered]@{ edit = $batchEdit; play = $batchPlay }
        sources = [ordered]@{
            checkedCount = $batchHashes.Count; matchingCount = $batchHashes.Count - $batchMismatches.Count
            mismatchingCount = $batchMismatches.Count; files = $batchHashes
        }
        errors = $batchErrors
    }
    [IO.Directory]::CreateDirectory($batchOutput) | Out-Null
    [IO.File]::WriteAllText($batchReportPath, ($batchReport | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    Write-Output ("Verification {0}: Edit {1}/{2}; Play {3}/{4}; matching sources {5}/{6}." -f
        $batchReport.success, $batchEdit.passed, $batchEdit.total, $batchPlay.passed, $batchPlay.total,
        $batchReport.sources.matchingCount, $batchReport.sources.checkedCount)
    Write-Output $batchReportPath
    if (-not $batchReport.success) { exit 1 }
    exit 0
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
