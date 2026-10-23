param([ValidateSet('Prepare','Capture')][string]$Stage = 'Prepare', [string]$Run = 'capture', [string]$Only = '')
$ErrorActionPreference = 'Stop'
$auditSource = 'D:\Game\UnityClient'
$auditProject = 'D:\Game\tmp\shipyard-assembly-20261017'
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple run name.' }
$auditOutput = 'D:\Game\Artifacts\SmallTextAudit20261022\' + $Run
$auditRoot = [IO.Path]::GetFullPath($auditProject)
if (-not $auditRoot.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Expected isolated tmp project.' }
$auditRunning = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($auditRoot)
})
if ($auditRunning.Count) { throw ('The selected scratch project is running: ' + ($auditRunning.ProcessId -join ', ')) }
$auditLock = Join-Path $auditRoot 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $auditLock) {
    try { $auditStream = [IO.File]::Open($auditLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $auditStream.Dispose() }
    catch { throw 'The scratch Unity project is locked.' }
}
[IO.Directory]::CreateDirectory($auditOutput) | Out-Null
if ($Stage -eq 'Prepare') {
    foreach ($auditPart in @('Assets','Packages','ProjectSettings')) {
        $auditCopyArgs = @((Join-Path $auditSource $auditPart), (Join-Path $auditRoot $auditPart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS',('/LOG+:' + (Join-Path $auditOutput 'sync.log')))
        if ($auditPart -eq 'Assets') { $auditCopyArgs += @('/XD', (Join-Path $auditSource 'Assets\_Project\Scenes\UIPreviewsForMe'), (Join-Path $auditRoot 'Assets\_Project\Scenes\UIPreviewsForMe')) }
        & robocopy @auditCopyArgs | Out-Null
        if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $auditPart) }
    }
    $auditAssets = [IO.Path]::GetFullPath((Join-Path $auditRoot 'Assets\_Project'))
    foreach ($auditOld in Get-ChildItem -LiteralPath $auditAssets -Filter '*.cs' -File -Recurse) {
        $auditRelative = $auditOld.FullName.Substring($auditAssets.Length + 1)
        if ($auditRelative.Replace('\','/').StartsWith('Scenes/UIPreviewsForMe/', [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (Test-Path -LiteralPath (Join-Path $auditSource ('Assets\_Project\' + $auditRelative))) { continue }
        $auditChecked = [IO.Path]::GetFullPath($auditOld.FullName)
        if (-not $auditChecked.StartsWith($auditAssets + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Scratch source path escaped.' }
        Rename-Item -LiteralPath $auditChecked -NewName ($auditOld.Name + '.smalltext-disabled')
    }
    $auditSettingsPath = Join-Path $auditRoot 'ProjectSettings\ProjectSettings.asset'
    $auditSettings = [IO.File]::ReadAllText($auditSettingsPath)
    $auditSettings = [regex]::Replace($auditSettings, '(?m)^  companyName:.*$', '  companyName: ShatteredSailSmallTextValidation')
    $auditSettings = [regex]::Replace($auditSettings, '(?m)^  productName:.*$', '  productName: Shattered Sail Small Text Audit')
    [IO.File]::WriteAllText($auditSettingsPath, $auditSettings, [Text.UTF8Encoding]::new($false))
    Write-Output 'Current assets and code synchronized to the isolated project.'
    exit 0
}
$auditCapture = Join-Path $PSScriptRoot 'Capture.cs'
if (-not (Test-Path -LiteralPath $auditCapture)) { throw 'Capture.cs is not ready.' }
Copy-Item -LiteralPath $auditCapture -Destination (Join-Path $auditRoot 'Assets\_Project\Editor\SmallTextCapture.cs') -Force
$auditPrepare = Join-Path $PSScriptRoot 'PrepareVariants.ps1'
if (Test-Path -LiteralPath $auditPrepare) { & $auditPrepare -ProjectPath $auditRoot }
$auditLog = Join-Path $auditOutput 'unity.log'
if (Test-Path -LiteralPath $auditLog) { throw 'Capture log already exists; preserve previous evidence.' }
$auditArgs = @('-batchmode','-force-d3d11','-projectPath',$auditRoot,'-logFile',$auditLog,'-executeMethod','ShatteredSail.Editor.SmallTextCapture.CaptureFromCommandLine','-smallTextOutput',$auditOutput,'-quit')
if ($Only) { $auditArgs += @('-smallTextOnly',$Only) }
$auditProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $auditArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{ PID=$auditProcess.Id; Project=$auditRoot; Output=$auditOutput; Arguments=$auditArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $auditOutput 'launch.json') -Encoding utf8
Write-Output ('Capture started: PID ' + $auditProcess.Id + '; ' + $auditLog)
