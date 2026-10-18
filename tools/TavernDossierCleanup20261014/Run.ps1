param([ValidateSet('Prepare','EditMode','PlayMode','Capture')][string]$Stage = 'Prepare', [string]$RunName = 'run1')
$ErrorActionPreference = 'Stop'
$dossierSource = 'D:\Game\UnityClient'
$dossierProject = 'D:\Game\tmp\departure-dock-scene-20261012'
$dossierOutput = 'D:\Game\Artifacts\TavernDossierCleanup20261014'
$dossierUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid run name.' }
if (-not (Test-Path -LiteralPath (Join-Path $dossierProject 'Library'))) { throw 'Missing isolated project.' }
if (Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($dossierProject) }) { throw 'Isolated project busy.' }
if ($Stage -eq 'Prepare') {
    foreach ($dossierFile in Get-ChildItem -LiteralPath (Join-Path $dossierSource 'Assets\_Project') -File -Recurse | Where-Object { $_.Extension -in @('.cs','.asmdef') }) {
        $dossierDestination = Join-Path $dossierProject $dossierFile.FullName.Substring($dossierSource.Length + 1)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dossierDestination)) | Out-Null
        Copy-Item -LiteralPath $dossierFile.FullName -Destination $dossierDestination -Force
        if (Test-Path -LiteralPath ($dossierFile.FullName + '.meta')) { Copy-Item -LiteralPath ($dossierFile.FullName + '.meta') -Destination ($dossierDestination + '.meta') -Force }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $dossierProject 'Assets/_Project/Editor/TavernDossierCleanupCapture.cs') -Force
    # The existing navigation test visits the adjacent component shop.
    foreach ($dossierCoverage in Get-ChildItem -LiteralPath (Join-Path $dossierSource 'Assets/_Project/Resources/ComponentShopRegional') -Filter '*_coverage.png*' -File) {
        Copy-Item -LiteralPath $dossierCoverage.FullName -Destination (Join-Path $dossierProject ('Assets/_Project/Resources/ComponentShopRegional/' + $dossierCoverage.Name)) -Force
    }
    foreach ($dossierSuffix in @('.png','.png.meta')) {
        $dossierBackground = 'Assets/_Project/Resources/PortFacilities/port_investment_scene_internal_v1' + $dossierSuffix
        Copy-Item -LiteralPath (Join-Path $dossierSource $dossierBackground) -Destination (Join-Path $dossierProject $dossierBackground) -Force
    }
    $dossierSettingsPath = Join-Path $dossierProject 'ProjectSettings/ProjectSettings.asset'
    $dossierSettings = [IO.File]::ReadAllText($dossierSettingsPath)
    foreach ($dossierKey in @('companyName','productName')) { $dossierSettings = [regex]::Replace($dossierSettings, '(?m)^  ' + $dossierKey + ':.*$', '  ' + $dossierKey + ': TavernDossierCleanupValidation20261014') }
    [IO.File]::WriteAllText($dossierSettingsPath, $dossierSettings, [Text.UTF8Encoding]::new($false))
    Write-Output 'Prepared isolated source and separate PlayerPrefs identity.'
    exit 0
}
$dossierRun = Join-Path $dossierOutput $RunName
if (Test-Path -LiteralPath $dossierRun) { throw 'Use a new run name.' }
[IO.Directory]::CreateDirectory($dossierRun) | Out-Null
$dossierArgs = @('-batchmode','-force-d3d11','-projectPath',$dossierProject,'-logFile',(Join-Path $dossierRun ($Stage + '.log')))
if ($Stage -eq 'Capture') {
    $env:TAVERN_CLEANUP_CAPTURE_DIR = Join-Path $dossierRun 'Screens'
    $dossierArgs += @('-quit','-executeMethod','ShatteredSail.Editor.TavernDossierCleanupCapture.Run')
} else {
    $dossierFilter = if ($Stage -eq 'EditMode') { 'TavernDossierLayoutTests;TavernDossierEditorPagingTests;SpecialCardEditorPreviewTests.InspectorAndConfigurationExposeAllFourCrewDossierPages;SpecialCardDossierTests.DoctorDossierShowsPersonalBoardingCardWithoutRecruiting;SpecialCardDossierTests.CookSpecialCardTab_ShowsNewPersonalCardWithoutRecruitingOrSpendingGold;SpecialCardDossierTests.PepeDossier_ShowsHisSupportCardWithoutRecruitingOrSpending;WoodButtonSkinTests.RuntimeFactoryAndEditorPreviewsUseTheSameSkin' } else { 'Scene_CompletesPortVoyageReturnLoop' }
    $dossierArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$dossierFilter,'-testResults',(Join-Path $dossierRun 'results.xml'))
}
$dossierQuoted = $dossierArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$dossierProcess = Start-Process -FilePath $dossierUnity -WindowStyle Hidden -ArgumentList $dossierQuoted -PassThru
Write-Output ('Unity ' + $Stage + ' PID ' + $dossierProcess.Id)
$dossierProcess.WaitForExit()
$dossierProcess.Refresh()
if ($dossierProcess.ExitCode -ne 0) { throw ('Unity failed; inspect ' + $dossierRun) }
if ($Stage -ne 'Capture') {
    [xml]$dossierResults = Get-Content -Raw -LiteralPath (Join-Path $dossierRun 'results.xml')
    if ($dossierResults.'test-run'.result -ne 'Passed' -or [int]$dossierResults.'test-run'.passed -le 0) { throw 'Tests failed or no selected tests ran.' }
    Write-Output ('Passed ' + $dossierResults.'test-run'.passed + ' tests.')
}
