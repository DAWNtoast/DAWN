param([string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$tooltipWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tooltipMain = Join-Path $tooltipWorkspace 'UnityClient/Assets/_Project/Resources'
$tooltipProject = Join-Path $tooltipWorkspace 'tmp/card-keyword-tooltip-20261023-validation'
$tooltipResources = Join-Path $tooltipProject 'Assets/Resources'
if (-not (Test-Path -LiteralPath (Join-Path $tooltipProject 'OWNED_CARD_KEYWORD_VALIDATION.txt'))) { throw 'Run the task test script first.' }
if (Test-Path -LiteralPath (Join-Path $tooltipProject 'Temp/UnityLockfile')) { throw 'The isolated tooltip project is already running.' }
$tooltipResourcesToCopy = @(
    'CardArt/Western/card_load_shot.png',
    'CardArt/Western/card_boarding_fire_11.png',
    'CardSchools/Art/card_school_fire_b08.png',
    'CardSchools/Art/card_school_fire_c01.png',
    'CardSchools/Frames/Integrated/frame-layouts.json',
    'PortFacilities/Market/market_shop_panel_v3.png'
)
$tooltipFrames = Join-Path $tooltipMain 'CardSchools/Frames/Integrated'
foreach ($tooltipPattern in @('frame-neutral-*.png','frame-fire-*.png')) {
    foreach ($tooltipFrame in Get-ChildItem -LiteralPath $tooltipFrames -Filter $tooltipPattern -File) {
        $tooltipResourcesToCopy += 'CardSchools/Frames/Integrated/' + $tooltipFrame.Name
    }
}
foreach ($tooltipRelativePath in $tooltipResourcesToCopy) {
    foreach ($tooltipSuffix in @('','.meta')) {
        $tooltipSource = Join-Path $tooltipMain ($tooltipRelativePath + $tooltipSuffix)
        if (-not (Test-Path -LiteralPath $tooltipSource)) { throw "Missing production capture resource: $tooltipSource" }
        $tooltipDestination = Join-Path $tooltipResources ($tooltipRelativePath + $tooltipSuffix)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $tooltipDestination) | Out-Null
        Copy-Item -LiteralPath $tooltipSource -Destination $tooltipDestination -Force
    }
}
$tooltipEditorFolder = Join-Path $tooltipProject 'Assets/Editor'
New-Item -ItemType Directory -Force -Path $tooltipEditorFolder | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination $tooltipEditorFolder -Force
$tooltipLog = Join-Path $tooltipWorkspace 'Artifacts/CardKeywordTooltip20261023/Capture.log'
$tooltipArguments = @('-batchmode','-projectPath',('"' + $tooltipProject + '"'),'-executeMethod','ShatteredSail.Editor.CardKeywordTooltipCapture.Run','-quit','-logFile',('"' + $tooltipLog + '"'))
$tooltipProcess = Start-Process -FilePath $UnityEditor -ArgumentList $tooltipArguments -WindowStyle Hidden -PassThru
$tooltipProcess.WaitForExit()
if ($tooltipProcess.ExitCode -ne 0) { throw "Tooltip capture failed. Inspect $tooltipLog" }
if (-not (Select-String -LiteralPath $tooltipLog -Pattern 'CARD_KEYWORD_TOOLTIP_CAPTURE_OK' -Quiet)) { throw 'Capture did not report completion.' }
Write-Output (Join-Path $tooltipWorkspace 'Artifacts/CardKeywordTooltip20261023/Capture')
