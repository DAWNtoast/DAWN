param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$ProjectPath = 'D:\Game\tmp\departure-dock-scene-20261012',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$guideSource = 'D:\Game\UnityClient'
$guideProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\')
$guideRoot = 'D:\Game\Artifacts\ConfirmationGuideStyle20261014\validation'
$guideUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
function Assert-GuideChild([string]$Path, [string]$Root) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    if (-not $resolved.StartsWith($parent + '\', [StringComparison]::OrdinalIgnoreCase)) { throw ('Path outside allowed root: ' + $resolved) }
    $cursor = $resolved
    while ($cursor -and $cursor.Length -ge $parent.Length) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Reparse path is not allowed: ' + $cursor) }
        }
        $cursor = Split-Path -Path $cursor -Parent
    }
    return $resolved
}
$guideProject = Assert-GuideChild $guideProject 'D:\Game\tmp'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath (Join-Path $guideProject 'Library'))) { throw 'A warm isolated project is required.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($guideProject) }).Count) { throw 'Isolated project is already running.' }
$guideUnityLock = Assert-GuideChild (Join-Path $guideProject 'Temp\UnityLockfile') $guideProject
if (Test-Path -LiteralPath $guideUnityLock) {
    try { $check = [IO.File]::Open($guideUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $check.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
$guideLease = $null
try {
    $guideLeasePath = Assert-GuideChild (Join-Path $guideProject 'ConfirmationGuideStyle20261014.lock') $guideProject
    $guideLease = [IO.File]::Open($guideLeasePath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $guideSettingsPath = Assert-GuideChild (Join-Path $guideProject 'ProjectSettings\ProjectSettings.asset') $guideProject
    if ($Stage -eq 'Prepare') {
        foreach ($part in @('Assets','Packages','ProjectSettings')) {
            $destination = Assert-GuideChild (Join-Path $guideProject $part) $guideProject
            $copy = @((Join-Path $guideSource $part), $destination, '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS','/XJ')
            if ($part -eq 'Assets') { $copy += @('/XD', (Join-Path $guideSource 'Assets\_Project\Scenes\UIPreviewsForMe')) }
            & robocopy @copy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $part) }
        }
        $scratch = Assert-GuideChild (Join-Path $guideProject 'Assets\_Project') $guideProject
        foreach ($old in @(Get-ChildItem -LiteralPath $scratch -Filter '*.cs' -File -Recurse)) {
            $relative = $old.FullName.Substring($scratch.Length + 1)
            if ($relative.StartsWith('Scenes\UIPreviewsForMe\', [StringComparison]::OrdinalIgnoreCase)) { continue }
            if (Test-Path -LiteralPath (Join-Path $guideSource ('Assets\_Project\' + $relative))) { continue }
            $resolved = Assert-GuideChild $old.FullName $scratch
            Remove-Item -LiteralPath $resolved -Force
            $meta = Assert-GuideChild ($resolved + '.meta') $scratch
            if (Test-Path -LiteralPath $meta) { Remove-Item -LiteralPath $meta -Force }
        }
        $guideCapture = Assert-GuideChild (Join-Path $guideProject 'Assets\_Project\Tests\PlayMode\ConfirmationGuideRuntimeCapture20261014.cs') $guideProject
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RuntimeCapture.cs') -Destination $guideCapture -Force
        $identity = 'ConfirmationGuideValidation20261014_' + [Guid]::NewGuid().ToString('N')
        $settings = [IO.File]::ReadAllText($guideSettingsPath)
        $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', ('  companyName: ' + $identity))
        $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', ('  productName: ' + $identity))
        [IO.File]::WriteAllText($guideSettingsPath, $settings, [Text.UTF8Encoding]::new($false))
        Write-Output ('Prepared current source and runtime capture with a fresh isolated PlayerPrefs identity: ' + $identity + '. No scenes generated.')
        exit 0
    }
    $settings = [IO.File]::ReadAllText($guideSettingsPath)
    foreach ($field in @('companyName','productName')) {
        if ($settings -notmatch ('(?m)^  ' + $field + ': ConfirmationGuideValidation20261014_[a-f0-9]{32}\r?$')) { throw 'Run Prepare first; production save identity is forbidden.' }
    }
    $guideOutput = Assert-GuideChild (Join-Path $guideRoot $RunName) $guideRoot
    if (Test-Path -LiteralPath $guideOutput) { throw 'Use a fresh RunName to preserve previous output.' }
    [IO.Directory]::CreateDirectory($guideOutput) | Out-Null
    $log = Assert-GuideChild (Join-Path $guideOutput ($Stage + '.log')) $guideOutput
    $guideArgs = @('-batchmode','-force-d3d11','-projectPath',$guideProject,'-logFile',$log)
    if ($Stage -eq 'Compile') { $guideArgs += '-quit' }
    else {
        if (-not $TestFilter) { $TestFilter = if ($Stage -eq 'EditMode') { 'MainMenuLayoutTests;GuidedTutorialBannerLayoutTests' } else { 'ConfirmationGuide_StyleRuntimeScreensAt1080pAnd720p' } }
        $results = Assert-GuideChild (Join-Path $guideOutput 'results.xml') $guideOutput
        $guideArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',$results)
        if ($Stage -eq 'PlayMode') {
            $screens = Assert-GuideChild (Join-Path $guideOutput 'Screens') $guideOutput
            $guideArgs += @('-confirmationGuideStyleOutput',$screens)
        }
    }
    $quoted = $guideArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $process = Start-Process -FilePath $guideUnity -WindowStyle Hidden -ArgumentList $quoted -PassThru
    $launch = Assert-GuideChild (Join-Path $guideOutput 'launch.json') $guideOutput
    [pscustomobject]@{ Stage=$Stage; ProcessId=$process.Id; Project=$guideProject; Arguments=$guideArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $launch -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $process.Id + '; output=' + $guideOutput)
    $process.WaitForExit(); $process.Refresh()
    if ($process.ExitCode -ne 0) { throw ('Unity failed with exit ' + $process.ExitCode + '; inspect ' + $guideOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$result = Get-Content -Raw -LiteralPath $results
        if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt 1) { throw ('No passing tests or test failure; inspect ' + $guideOutput) }
        Write-Output ('Passed ' + $result.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $guideOutput)
} finally { if ($guideLease) { $guideLease.Dispose() } }
