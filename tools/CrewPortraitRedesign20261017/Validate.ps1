param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Test','Capture')][string]$Stage,
    [string]$RunName = 'run1',
    [string]$TestFilter = 'ArtCatalogLazyLoadingTests.CrewDossiers_LoadOnlyTheRequestedIdentityAndReleaseBorrowedReferences',
    [string]$CrewId = ''
)
$ErrorActionPreference = 'Stop'
$portraitRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$portraitSource = Join-Path $portraitRoot 'UnityClient'
$portraitProject = Join-Path $portraitRoot 'tmp/crew-portraits-20261017'
$portraitCache = Join-Path $portraitRoot 'tmp/departure-dock-scene-20261012/Library'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid run name.' }
if ($portraitProject -ne 'D:\Game\tmp\crew-portraits-20261017') { throw 'Only the dedicated validation project is allowed.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($portraitProject)
}).Count) { throw 'Portrait validation project is running.' }
if ($Stage -eq 'Prepare') {
    [IO.Directory]::CreateDirectory($portraitProject) | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $portraitProject 'Library'))) {
        & robocopy $portraitCache (Join-Path $portraitProject 'Library') /E /R:1 /W:1 /MT:8 /NFL /NDL /NP /NJH /NJS | Out-Null
        if ($LASTEXITCODE -ge 8) { throw 'Warm Library copy failed.' }
    }
    foreach ($portraitPart in @('Assets','Packages','ProjectSettings')) {
        $portraitCopy = @((Join-Path $portraitSource $portraitPart), (Join-Path $portraitProject $portraitPart), '/E','/R:1','/W:1','/MT:8','/NFL','/NDL','/NP','/NJH','/NJS')
        if ($portraitPart -eq 'Assets') {
            $portraitCopy += @('/XD',(Join-Path $portraitSource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta')
        }
        & robocopy @portraitCopy | Out-Null
        if ($LASTEXITCODE -ge 8) { throw ('Source copy failed: ' + $portraitPart) }
    }
    $portraitSettingsPath = Join-Path $portraitProject 'ProjectSettings/ProjectSettings.asset'
    $portraitSettings = [IO.File]::ReadAllText($portraitSettingsPath)
    $portraitSettings = [regex]::Replace($portraitSettings, '(?m)^  companyName:.*$', '  companyName: CodexCrewPortraitValidation')
    $portraitSettings = [regex]::Replace($portraitSettings, '(?m)^  productName:.*$', '  productName: CrewPortraitValidation20261017')
    [IO.File]::WriteAllText($portraitSettingsPath, $portraitSettings, [Text.UTF8Encoding]::new($false))
    Write-Output ('Prepared dedicated project: ' + $portraitProject)
    exit 0
}
if (Test-Path -LiteralPath (Join-Path $portraitProject 'Assets/_Project/Scenes/UIPreviewsForMe')) {
    throw 'Retired previews must not exist in the validation project.'
}
$portraitOutput = Join-Path $portraitRoot ('Artifacts/CrewPortraitRedesign20261017/' + $RunName)
if (Test-Path -LiteralPath $portraitOutput) { throw 'Use a fresh run name; existing evidence is preserved.' }
[IO.Directory]::CreateDirectory($portraitOutput) | Out-Null
$portraitArgs = @('-batchmode','-force-d3d11','-projectPath',$portraitProject,'-logFile',(Join-Path $portraitOutput 'unity.log'))
if ($Stage -eq 'Capture') {
    $portraitArgs += @('-quit','-executeMethod','ShatteredSail.Editor.IntegratedCrewDossierAcceptance.CaptureFromCommandLine',
        '-dossierReviewOutput',(Join-Path $portraitOutput 'captures'))
    if ($CrewId) { $portraitArgs += @('-dossierReviewCrew',$CrewId) }
} else {
    $portraitArgs += @('-runTests','-testPlatform','EditMode','-testFilter',$TestFilter,'-testResults',(Join-Path $portraitOutput 'results.xml'))
}
$portraitQuoted = $portraitArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$portraitProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $portraitQuoted -WorkingDirectory $portraitProject -WindowStyle Hidden -PassThru
[ordered]@{ processId = $portraitProcess.Id; stage = $Stage; project = $portraitProject; arguments = $portraitArgs; startedUtc = [DateTime]::UtcNow.ToString('O') } |
    ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $portraitOutput 'launch.json') -Encoding utf8
Write-Output ('Started ' + $Stage + ', PID ' + $portraitProcess.Id + '; inspect ' + $portraitOutput)
