param(
    [string]$RunName = 'run1',
    [ValidateSet('EditMode','PlayMode')][string[]]$Modes = @('EditMode','PlayMode'),
    [string]$PlayFilter = 'DepartureBasicSupplies_DirectInputAndTenStepsQuoteExactDiscountedBasket;DepartureBasicSupplies_InvalidInputCannotBuyAndRetiredInputCannotChangeNewBasket;DepartureBasicSupplies_BulkInputClampsQuotaAndPreservesCapacityValidation;RecommendedSupplyUi_'
)
$ErrorActionPreference = 'Stop'
$supplyWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$supplyMain = Join-Path $supplyWorkspace 'UnityClient/Assets/_Project'
$supplyProject = Join-Path $supplyWorkspace 'tmp/window-fit-20261025'
$supplyOutput = Join-Path $supplyWorkspace ('Artifacts/BasicSupplyLayout20261025/' + $RunName)
$supplyUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Use a simple run name.' }
if (Test-Path -LiteralPath $supplyOutput) { throw 'Use a new run name to preserve evidence.' }
if (!(Test-Path -LiteralPath (Join-Path $supplyProject 'Library'))) { throw 'The shared warm validation project is required.' }
function Assert-SupplyProjectIdle {
    if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($supplyProject.Replace('/','\'))
    }).Count) { throw 'The shared validation project is in use.' }
    if (Test-Path -LiteralPath (Join-Path $supplyProject 'Temp/UnityLockfile')) { throw 'The shared validation project has a lockfile.' }
}
Assert-SupplyProjectIdle
foreach ($supplyPath in @($supplyProject, (Join-Path $supplyProject 'Assets'), (Join-Path $supplyProject 'Assets/_Project'))) {
    if ((Get-Item -LiteralPath $supplyPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Validation path must not be a directory link.' }
}
$supplySettings = [IO.File]::ReadAllText((Join-Path $supplyProject 'ProjectSettings/ProjectSettings.asset'))
if ($supplySettings -notmatch 'companyName: CodexWindowFitValidation' -or $supplySettings -notmatch 'productName: WindowFit20261025') {
    throw 'Validation must use separate PlayerPrefs.'
}
$supplyLease = [IO.File]::Open((Join-Path $supplyProject 'BasicSupplyLayout20261025.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$supplyChanges = [Collections.Generic.List[object]]::new()
$supplySourceHashes = [Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path $supplyOutput | Out-Null
try {
    foreach ($supplyPart in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Tests/PlayMode','Tests/EditMode')) {
        foreach ($supplyFile in Get-ChildItem -LiteralPath (Join-Path $supplyMain $supplyPart) -Filter '*.cs' -File -Recurse) {
            $supplyRelative = $supplyFile.FullName.Substring($supplyMain.Length + 1)
            $supplyTarget = Join-Path $supplyProject ('Assets/_Project/' + $supplyRelative)
            $supplyHash = (Get-FileHash -LiteralPath $supplyFile.FullName).Hash
            $supplySourceHashes.Add([pscustomobject]@{ Path=$supplyFile.FullName; SHA256=$supplyHash })
            if (!(Test-Path -LiteralPath $supplyTarget)) { throw ('Missing shared source: ' + $supplyRelative) }
            if ((Get-FileHash -LiteralPath $supplyTarget).Hash -eq $supplyHash) { continue }
            $supplyBackup = Join-Path $supplyOutput ('OriginalValidationFiles/' + $supplyRelative)
            New-Item -ItemType Directory -Force -Path (Split-Path $supplyBackup -Parent) | Out-Null
            Copy-Item -LiteralPath $supplyTarget -Destination $supplyBackup
            $supplyChanges.Add([pscustomobject]@{ Target=$supplyTarget; Backup=$supplyBackup })
            Copy-Item -LiteralPath $supplyFile.FullName -Destination $supplyTarget -Force
        }
    }
    $supplySourceHashes | Export-Csv -LiteralPath (Join-Path $supplyOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    foreach ($supplyMode in $Modes) {
        $supplyFilter = if ($supplyMode -eq 'EditMode') { 'DepartureBasicSuppliesLayoutTests' } else {
            $PlayFilter
        }
        $supplyResult = Join-Path $supplyOutput ($supplyMode + '.xml')
        $supplyArgs = @('-batchmode','-force-d3d11','-projectPath',$supplyProject,'-runTests','-testPlatform',$supplyMode,
            '-testFilter',$supplyFilter,'-testResults',$supplyResult,'-logFile',(Join-Path $supplyOutput ($supplyMode + '.log')))
        if ($supplyMode -eq 'PlayMode') { $supplyArgs += @('-basicSupplyAuditPath',(Join-Path $supplyOutput 'Screens')) }
        $supplyProcess = Start-Process -FilePath $supplyUnity -ArgumentList ($supplyArgs | ForEach-Object { '"' + $_ + '"' }) -WindowStyle Hidden -PassThru
        Write-Output ($supplyMode + ' running, PID ' + $supplyProcess.Id)
        $supplyProcess.WaitForExit()
        $supplyProcess.Refresh()
        if (!(Test-Path -LiteralPath $supplyResult)) { throw ($supplyMode + ' produced no results; inspect log.') }
        [xml]$supplyResults = Get-Content -Raw -LiteralPath $supplyResult
        $supplyRun = $supplyResults.'test-run'
        Write-Output ($supplyMode + ': ' + $supplyRun.result + ', passed=' + $supplyRun.passed + ', failed=' + $supplyRun.failed)
        if ($supplyProcess.ExitCode -ne 0 -or $supplyRun.result -ne 'Passed' -or [int]$supplyRun.passed -lt 1) { throw ($supplyMode + ' failed.') }
    }
} finally {
    Assert-SupplyProjectIdle
    foreach ($supplyChange in $supplyChanges) {
        Copy-Item -LiteralPath $supplyChange.Backup -Destination $supplyChange.Target -Force
        if ((Get-FileHash -LiteralPath $supplyChange.Backup).Hash -ne (Get-FileHash -LiteralPath $supplyChange.Target).Hash) { throw 'Shared source restoration failed.' }
    }
    $supplyLease.Dispose()
    Remove-Item -LiteralPath (Join-Path $supplyProject 'BasicSupplyLayout20261025.lock') -Force
    $supplyChangedMain = @($supplySourceHashes | Where-Object { (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 })
    $supplyTempRemaining = Test-Path -LiteralPath (Join-Path $supplyProject 'Temp')
    @('Shared validation sources restored.',
        'No full project or new Library was created. Shared Library retained for window-fit validation and existing tools.',
        ('Production sources changed during validation/cleanup: ' + $supplyChangedMain.Count),
        ('Temp directory remaining after Unity exit: ' + $supplyTempRemaining),
        'Task lease removed (0 bytes); test results, screenshots and original source evidence retained.') |
        Set-Content -LiteralPath (Join-Path $supplyOutput 'cleanup.txt') -Encoding utf8
    if ($supplyChangedMain.Count) { throw 'Production sources changed during validation; inspect source-hashes.csv.' }
}
