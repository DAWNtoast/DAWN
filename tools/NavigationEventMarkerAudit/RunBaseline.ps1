$ErrorActionPreference = 'Stop'
$taskSource = 'D:\Game\UnityClient'
$taskProject = 'D:\Game\tmp\navigation-map-label-clean-20261012'
$taskOutput = 'D:\Game\Artifacts\NavigationEventMarker20261013'
$taskUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$taskRelativeFiles = @(
    'Assets\_Project\Presentation\Runtime\CoreGameplayVoyageMapPresenter.cs',
    'Assets\_Project\Presentation\Runtime\CoreGameplayDemoController.cs'
)
if (-not ([IO.Path]::GetFullPath($taskProject)).StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid scratch project.' }
if (Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskProject) }) { throw 'Scratch project is running.' }
[IO.Directory]::CreateDirectory((Join-Path $taskOutput 'baseline-fixed-source')) | Out-Null
$taskFixedBytes = @{}
$taskFixedHashes = @{}
foreach ($taskRelative in $taskRelativeFiles) {
    $taskPath = Join-Path $taskProject $taskRelative
    $taskHash = (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash
    if ($taskHash -ne (Get-FileHash -LiteralPath (Join-Path $taskSource $taskRelative) -Algorithm SHA256).Hash) { throw "Scratch does not match fixed production: $taskRelative" }
    $taskFixedBytes[$taskRelative] = [IO.File]::ReadAllBytes($taskPath)
    $taskFixedHashes[$taskRelative] = $taskHash
    [IO.File]::WriteAllBytes((Join-Path $taskOutput ('baseline-fixed-source\' + [IO.Path]::GetFileName($taskRelative))), $taskFixedBytes[$taskRelative])
}

function Replace-ExactOnce([string]$text, [string]$oldValue, [string]$newValue) {
    if (($text.Length - $text.Replace($oldValue, '').Length) / $oldValue.Length -ne 1) { throw 'Baseline source anchor is not unique.' }
    return $text.Replace($oldValue, $newValue)
}

$taskResults = @()
try {
    $taskMapPath = Join-Path $taskProject $taskRelativeFiles[0]
    $taskMapText = [Text.Encoding]::UTF8.GetString($taskFixedBytes[$taskRelativeFiles[0]])
    $taskEol = if ($taskMapText.Contains("`r`n")) { "`r`n" } else { "`n" }
    $taskClearBlock = @(
        '            // Keep the full button hit area, but let the artwork''s transparent',
        '            // edges show the sea instead of a dark, disabled-state button frame.',
        '            if (marker.targetGraphic is Image frame)',
        '            {',
        '                frame.color = Color.clear;',
        '            }',
        '',
        ''
    ) -join $taskEol
    $taskMapText = Replace-ExactOnce $taskMapText $taskClearBlock ''
    $taskFrameBlock = @(
        '            Image frame = marker.targetGraphic as Image;',
        '            if (frame != null)',
        '            {',
        '                Outline frameOutline = frame.gameObject.AddComponent<Outline>();',
        '                frameOutline.effectColor = new Color(0.03f, 0.08f, 0.09f, 0.9f);',
        '                frameOutline.effectDistance = new Vector2(2f, -2f);',
        '                frameOutline.useGraphicAlpha = true;',
        '            }',
        '',
        ''
    ) -join $taskEol
    $taskGlyphAnchor = '            Text glyph = marker.GetComponentInChildren<Text>();'
    $taskMapText = Replace-ExactOnce $taskMapText $taskGlyphAnchor ($taskFrameBlock + $taskGlyphAnchor)

    $taskControllerPath = Join-Path $taskProject $taskRelativeFiles[1]
    $taskControllerText = [Text.Encoding]::UTF8.GetString($taskFixedBytes[$taskRelativeFiles[1]])
    $taskControllerEol = if ($taskControllerText.Contains("`r`n")) { "`r`n" } else { "`n" }
    $taskDecorateLine = '                if (!treasureCore) DecorateVoyageEventMarker(marker, node);' + $taskControllerEol
    $taskControllerText = Replace-ExactOnce $taskControllerText $taskDecorateLine ''
    $taskInteractableLine = '                marker.interactable = session.Phase == CoreDemoPhase.Sailing && !node.Visited;'
    $taskControllerText = Replace-ExactOnce $taskControllerText $taskInteractableLine ($taskDecorateLine + $taskInteractableLine)
    [IO.File]::WriteAllText($taskMapPath, $taskMapText, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($taskControllerPath, $taskControllerText, [Text.UTF8Encoding]::new($false))
    Write-Output 'Reverted only the event marker fix in the isolated scratch project.'

    foreach ($taskStage in @('EditMode','PlayMode')) {
        $taskFilter = if ($taskStage -eq 'EditMode') { 'SeedVisibilityPresentationTests.Departure_ChartModeReplacesLeftRoutesAndShowsSelectedChartAndDirectionalPorts' } else { 'ChartSeed_EntryUnlocksAfterRecruitmentButNavigationKeepsItHidden_AndOnboardIncidentsNeverBecomeMarkers' }
        $taskXml = Join-Path $taskOutput ('Baseline' + $taskStage + '.xml')
        $taskArgs = @('-batchmode','-projectPath',$taskProject,'-logFile',(Join-Path $taskOutput ('Baseline' + $taskStage + '.log')),
            '-runTests','-testPlatform',$taskStage,'-testFilter',$taskFilter,'-testResults',$taskXml)
        $taskQuotedArgs = $taskArgs | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
        $taskProcess = Start-Process -FilePath $taskUnity -ArgumentList $taskQuotedArgs -WindowStyle Hidden -PassThru
        Write-Output ("Baseline $taskStage Unity PID " + $taskProcess.Id)
        $taskProcess.WaitForExit()
        $taskResult = [ordered]@{ Stage=$taskStage; ProcessId=$taskProcess.Id; ExitCode=$taskProcess.ExitCode; Xml=$taskXml }
        if (Test-Path -LiteralPath $taskXml) {
            [xml]$taskDocument = [IO.File]::ReadAllText($taskXml)
            $taskResult.Tests = @($taskDocument.SelectNodes('//test-case') | ForEach-Object {
                [ordered]@{ Name=$_.fullname; Result=$_.result; Message=$_.SelectSingleNode('failure/message').InnerText; Stack=$_.SelectSingleNode('failure/stack-trace').InnerText }
            })
        }
        $taskResults += [pscustomobject]$taskResult
        $taskResult | ConvertTo-Json -Depth 5
    }
}
finally {
    $taskRestored = @()
    foreach ($taskRelative in $taskRelativeFiles) {
        $taskPath = Join-Path $taskProject $taskRelative
        [IO.File]::WriteAllBytes($taskPath, $taskFixedBytes[$taskRelative])
        $taskRestoredHash = (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash
        $taskProductionHash = (Get-FileHash -LiteralPath (Join-Path $taskSource $taskRelative) -Algorithm SHA256).Hash
        $taskRestored += [pscustomobject]@{ File=$taskRelative; BeforeSha256=$taskFixedHashes[$taskRelative]; RestoredSha256=$taskRestoredHash; ProductionSha256=$taskProductionHash; MatchesBefore=($taskRestoredHash -eq $taskFixedHashes[$taskRelative]); MatchesProduction=($taskRestoredHash -eq $taskProductionHash) }
    }
    [pscustomobject]@{ Results=$taskResults; RestoredSources=$taskRestored } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'BaselineSummary.json') -Encoding utf8
    $taskRestored | ConvertTo-Json -Depth 4
    if ($taskRestored | Where-Object { -not $_.MatchesBefore -or -not $_.MatchesProduction }) { throw 'Restored scratch source hash mismatch.' }
}
