param(
    [ValidateSet('Capture','EditMode')]
    [string]$Action = 'Capture',
    [string]$RunName = 'comparison',
    [string]$ProjectPath = 'D:/Game/tmp/card-clarity-20261013',
    [string]$OutputRoot = 'D:/Game/Artifacts/BrokenAnchorCrewAtlas4K20261013/verification'
)
$ErrorActionPreference = 'Stop'

# This runner neither syncs project files nor installs candidate art. The caller prepares
# the isolated project after agreeing on the runtime comparison API.
$crewUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$crewProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$crewTemporaryRoot = [IO.Path]::GetFullPath('D:/Game/tmp').TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (-not ($crewProject + [IO.Path]::DirectorySeparatorChar).StartsWith($crewTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Only an isolated project beneath D:/Game/tmp is allowed.'
}
if (-not (Test-Path -LiteralPath (Join-Path $crewProject 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The isolated Unity project does not exist.'
}
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple directory name.' }
if (-not [IO.Path]::IsPathRooted($OutputRoot)) { throw 'OutputRoot must be absolute.' }
$crewOutput = [IO.Path]::GetFullPath((Join-Path $OutputRoot $RunName))
$crewProductRoot = [IO.Path]::GetFullPath('D:/Game/UnityClient').TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (($crewOutput + [IO.Path]::DirectorySeparatorChar).StartsWith($crewProject + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    ($crewOutput + [IO.Path]::DirectorySeparatorChar).StartsWith($crewProductRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be outside Unity projects.'
}

# Never open a competing editor or remove a possibly live lockfile.
$crewEditors = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
if ($crewEditors.Count -ne 0) {
    throw ('A Unity editor is already running (PID ' + (($crewEditors | ForEach-Object { $_.Id }) -join ', ') + '). Run after it exits.')
}
if (Test-Path -LiteralPath (Join-Path $crewProject 'Temp/UnityLockfile')) {
    throw 'The isolated project has a Unity lockfile; inspect it before running.'
}
if (-not (Test-Path -LiteralPath $crewUnity)) { throw 'Expected Unity editor executable is missing.' }

$crewLog = Join-Path $crewOutput ($Action.ToLowerInvariant() + '.log')
if (Test-Path -LiteralPath $crewLog) { throw 'Log already exists; choose a new RunName.' }
$crewArguments = @('-batchmode','-force-d3d11','-buildTarget','Win64','-projectPath',$crewProject,'-logFile',$crewLog)
if ($Action -eq 'Capture') {
    if (-not (Test-Path -LiteralPath (Join-Path $crewProject 'Assets/_Project/Editor/CrewAtlasCapture.cs'))) {
        throw 'Install the completed CrewAtlasCapture.cs in the isolated Editor folder first.'
    }
    $crewCapture = Join-Path $crewOutput 'capture'
    if (Test-Path -LiteralPath $crewCapture) { throw 'Capture output already exists; choose a new RunName.' }
    $crewPreviousOutput = $env:CREW_ATLAS_CAPTURE_DIR
    $env:CREW_ATLAS_CAPTURE_DIR = $crewCapture
    $crewArguments += @('-quit','-executeMethod','ShatteredSail.Editor.CrewAtlasCapture.Run')
}
else {
    $crewResults = Join-Path $crewOutput 'editmode.xml'
    if (Test-Path -LiteralPath $crewResults) { throw 'Test output already exists; choose a new RunName.' }
    $crewArguments += @('-runTests','-testPlatform','EditMode','-testFilter',
        'TavernDossierLayoutTests;TavernDossierEditorPagingTests;TavernRosterIntegrityTests;TavernRegionalInteractionLayoutTests;ArtCatalogLazyLoadingTests',
        '-testResults',$crewResults)
}
New-Item -ItemType Directory -Path $crewOutput -Force | Out-Null
try {
    # Quote every argument: Start-Process joins the array into one command line.
    $crewQuotedArguments = $crewArguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $crewProcess = Start-Process -FilePath $crewUnity -WindowStyle Hidden -ArgumentList $crewQuotedArguments -PassThru
    Write-Output ('Unity ' + $Action + ' PID=' + $crewProcess.Id + '; log=' + $crewLog)
    $crewProcess.WaitForExit()
    $crewProcess.Refresh()
    $crewExit = $crewProcess.ExitCode
    Write-Output ('Unity ' + $Action + ' exit=' + $crewExit)
    if ($crewExit -ne 0) { throw ('Unity failed. See ' + $crewLog) }
    if ($Action -eq 'Capture') {
        $crewReport = Get-Content -Raw -LiteralPath (Join-Path $crewCapture 'report.json') | ConvertFrom-Json
        if ($crewReport.status -ne 'passed') { throw 'Capture report did not pass.' }
    }
    else {
        [xml]$crewTestReport = Get-Content -Raw -LiteralPath $crewResults
        if ($crewTestReport.'test-run'.result -ne 'Passed' -or [int]$crewTestReport.'test-run'.failed -ne 0) {
            throw 'EditMode tests did not pass.'
        }
    }
}
finally {
    if ($Action -eq 'Capture') { $env:CREW_ATLAS_CAPTURE_DIR = $crewPreviousOutput }
}
