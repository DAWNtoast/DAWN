param(
    [ValidateSet('Prepare','Assets','Compile','Edit','Capture')][string]$Stage,
    [string]$Run = 'initial',
    [string]$ProjectPath = 'D:/Game/tmp/component-rarity-halo-20261019',
    [string]$TestFilter = 'CardSupplyCostPresentationTests;CardResourceBadgeTests;BattleCardFrameGraphicTests;BattleCardTextLayoutTests'
)
$ErrorActionPreference = 'Stop'
$supplySource = 'D:\Game\UnityClient'
$supplyProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
if (-not $supplyProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an existing isolated workspace/tmp project.' }
if (-not (Test-Path -LiteralPath (Join-Path $supplyProject 'ProjectSettings/ProjectVersion.txt'))) { throw 'Existing isolated project required.' }
$supplyRetired = Join-Path $supplyProject 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $supplyRetired) -and @(Get-ChildItem -LiteralPath $supplyRetired -Force).Count) { throw 'Choose an isolated project without retired preview content.' }
$supplyProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($supplyProject.Replace('\','/')) })
if ($supplyProcesses.Count) { throw 'The isolated project is occupied by Unity.' }
$supplyOutput = 'D:\Game\Artifacts\CardSupplyFrames20261021\' + $Run
[IO.Directory]::CreateDirectory($supplyOutput) | Out-Null
$script:supplyCopied = [Collections.Generic.List[string]]::new()
function Copy-SupplyFile([string]$relative) {
    if ($relative -match '(^|[/\\])UIPreviewsForMe([/\\]|$)') { throw 'Retired previews are excluded.' }
    $sourcePath = [IO.Path]::GetFullPath((Join-Path $supplySource $relative))
    $targetPath = [IO.Path]::GetFullPath((Join-Path $supplyProject $relative))
    if (-not $sourcePath.StartsWith($supplySource + '\', [StringComparison]::OrdinalIgnoreCase) -or -not $targetPath.StartsWith($supplyProject + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe sync path.' }
    if ((Test-Path -LiteralPath $targetPath) -and (Get-FileHash -LiteralPath $sourcePath).Hash -eq (Get-FileHash -LiteralPath $targetPath).Hash) { return }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetPath)) | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    $script:supplyCopied.Add($relative)
}
if ($Stage -eq 'Prepare') {
    foreach ($directory in @('Core','Content','Persistence','Presentation/Runtime','Tests')) {
        foreach ($sourceFile in Get-ChildItem -LiteralPath (Join-Path $supplySource ('Assets/_Project/' + $directory)) -Recurse -File) {
            if ($sourceFile.Extension -in @('.cs','.asmdef','.meta')) { Copy-SupplyFile $sourceFile.FullName.Substring($supplySource.Length + 1) }
        }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $supplyProject 'Assets/_Project/Editor/CardSupplyFrameCapture.cs') -Force
    $settingsPath = Join-Path $supplyProject 'ProjectSettings/ProjectSettings.asset'
    $settings = [IO.File]::ReadAllText($settingsPath)
    $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: CodexCardSupplyValidation')
    $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: CardSupplyFrames20261021')
    [IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
    $script:supplyCopied | Set-Content -LiteralPath (Join-Path $supplyOutput 'code-sync.txt') -Encoding utf8
    Write-Output ('Copied only changed code files: ' + $script:supplyCopied.Count)
    return
}
if ($Stage -eq 'Assets') {
    foreach ($directory in @('CardSchools/Frames','ItemIcons')) {
        foreach ($sourceFile in Get-ChildItem -LiteralPath (Join-Path $supplySource ('Assets/_Project/Resources/' + $directory)) -Recurse -File) {
            Copy-SupplyFile $sourceFile.FullName.Substring($supplySource.Length + 1)
        }
    }
    $script:supplyCopied | Set-Content -LiteralPath (Join-Path $supplyOutput 'asset-sync.txt') -Encoding utf8
    Write-Output ('Copied only changed frame/icon files: ' + $script:supplyCopied.Count)
    return
}
$supplyEditor = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$supplyLog = Join-Path $supplyOutput ($Stage.ToLowerInvariant() + '.log')
if (Test-Path -LiteralPath $supplyLog) { throw 'Use a new Run value to preserve evidence.' }
$supplyArguments = @('-batchmode','-projectPath',('"' + $supplyProject + '"'),'-logFile',('"' + $supplyLog + '"'))
if ($Stage -eq 'Compile') { $supplyArguments += @('-nographics','-quit') }
if ($Stage -eq 'Edit') { $supplyArguments += @('-nographics','-runTests','-testPlatform','EditMode','-testFilter',('"' + $TestFilter + '"'),'-testResults',('"' + (Join-Path $supplyOutput 'edit-results.xml') + '"')) }
if ($Stage -eq 'Capture') { $supplyArguments += @('-force-d3d11','-executeMethod','ShatteredSail.Editor.CardSupplyFrameCapture.CaptureFromCommandLine','-cardSupplyOutput',('"' + (Join-Path $supplyOutput 'captures') + '"'),'-quit') }
$supplyProcess = Start-Process -FilePath $supplyEditor -ArgumentList $supplyArguments -WorkingDirectory $supplyProject -WindowStyle Hidden -PassThru
[ordered]@{stage=$Stage;processId=$supplyProcess.Id;log=$supplyLog;arguments=$supplyArguments;startedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $supplyOutput ($Stage.ToLowerInvariant() + '-launch.json')) -Encoding utf8
Write-Output ('Started ' + $Stage + ' PID ' + $supplyProcess.Id + ' log ' + $supplyLog)
