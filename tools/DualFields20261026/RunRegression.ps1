param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'DualFields20261026Regression')
$ErrorActionPreference = 'Stop'
$dualRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dualArtifacts = Join-Path $dualRoot 'Artifacts/BossCalibration20261024'
$dualOutput = Join-Path $dualArtifacts $RunTag
$dualBuild = Join-Path $dualOutput 'Build'
$dualSharedRunner = Join-Path $dualRoot 'tools/BossCalibration20261024/RunCoreTests.ps1'
$dualTokens = $null
$dualParseErrors = $null
$dualAst = [Management.Automation.Language.Parser]::ParseFile($dualSharedRunner, [ref]$dualTokens, [ref]$dualParseErrors)
if ($dualParseErrors.Count) { throw 'Cannot read the shared regression fixture list.' }
$dualParameter = $dualAst.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'Fixtures' }
$dualFixtures = @($dualParameter.DefaultValue.SafeGetValue()) + @('CoreNavalDualField20261026Tests', 'CoreNavalBossRush20261026Tests', 'CoreBoardingDualField20261026Tests', 'CoreBoardingFieldBalance20261024Tests')
$dualFixtures = @($dualFixtures | Select-Object -Unique)
try {
    & (Join-Path $dualRoot 'tools/BossCalibration20261024/RunCoreTests.ps1') -RunTag $RunTag -Fixtures $dualFixtures
} finally {
    # The synchronous test runner has exited; preserve all logs, XML and source hashes.
    $dualFreed = 0L
    foreach ($dualPath in @($dualRoot, (Join-Path $dualRoot 'Artifacts'), $dualArtifacts, $dualOutput, $dualBuild)) {
        if ((Test-Path -LiteralPath $dualPath) -and
            ((Get-Item -LiteralPath $dualPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing cleanup through a directory link: ' + $dualPath)
        }
    }
    $dualBuildFull = [IO.Path]::GetFullPath($dualBuild)
    $dualAllowed = [IO.Path]::GetFullPath($dualArtifacts).TrimEnd('\') + '\'
    if (-not $dualBuildFull.StartsWith($dualAllowed, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The test build directory escaped its task artifact root.'
    }
    $dualBusy = @(Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and $_.CommandLine.IndexOf($dualBuildFull, [StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($dualBusy.Count -gt 0) { throw 'A process still references the test build; cleanup was deferred.' }
    foreach ($dualName in @('NavalBalanceTests.exe', 'nunit.framework.dll')) {
        $dualFile = Join-Path $dualBuildFull $dualName
        if (Test-Path -LiteralPath $dualFile) {
            $dualInfo = Get-Item -LiteralPath $dualFile -Force
            if ($dualInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Refusing cleanup of link: ' + $dualFile) }
            $dualFreed += $dualInfo.Length
            Remove-Item -LiteralPath $dualFile
        }
    }
    if ((Test-Path -LiteralPath $dualBuildFull) -and @(Get-ChildItem -LiteralPath $dualBuildFull -Force).Count -eq 0) {
        Remove-Item -LiteralPath $dualBuildFull
    }
    if (Test-Path -LiteralPath $dualOutput) {
        [pscustomobject]@{ FreedBytes = $dualFreed; BuildRemoved = -not (Test-Path -LiteralPath $dualBuildFull);
            Preserved = 'Test logs, results.xml, fixtures and source hashes'; MainProjectCleanup = $false } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dualOutput 'cleanup.json') -Encoding utf8
    }
    Write-Output ('Task test-cache cleanup released ' + $dualFreed + ' bytes; UnityClient was not cleaned.')
}
