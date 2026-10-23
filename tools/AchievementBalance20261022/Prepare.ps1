param([string]$RunTag = 'current', [string]$ProjectPath = 'D:/Game/tmp/component-rarity-halo-20261019')
$ErrorActionPreference = 'Stop'
$achievementWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$achievementMain = Join-Path $achievementWorkspace 'UnityClient'
$achievementProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$achievementOutput = Join-Path $achievementWorkspace ('Artifacts/AchievementBalance20261022/' + $RunTag)
if (-not $achievementProject.StartsWith((Join-Path $achievementWorkspace 'tmp') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated workspace/tmp project.' }
if (-not (Test-Path -LiteralPath (Join-Path $achievementProject 'ProjectSettings/ProjectVersion.txt'))) { throw 'An existing isolated Unity project is required.' }
$achievementRetired = Join-Path $achievementProject 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $achievementRetired) -and @(Get-ChildItem -LiteralPath $achievementRetired -Force).Count) { throw 'Choose an isolated project without retired preview content.' }
if (Test-Path -LiteralPath (Join-Path $achievementProject 'Temp/UnityLockfile')) { throw 'The isolated project is already running.' }
if (@(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($achievementProject) }).Count) { throw 'The isolated project is occupied by Unity.' }
$achievementSettingsPath = Join-Path $achievementProject 'ProjectSettings/ProjectSettings.asset'
$achievementSettings = [IO.File]::ReadAllText($achievementSettingsPath)
if ($achievementSettings -notmatch '(?m)^  companyName: .*Validation.*$') { throw 'The existing project must already use an isolated validation storage identity.' }
New-Item -ItemType Directory -Force -Path $achievementOutput | Out-Null
$script:achievementCopied = [Collections.Generic.List[string]]::new()
foreach ($achievementDirectory in @('Core','Content','Persistence','Presentation/Runtime','Tests')) {
    foreach ($achievementFile in Get-ChildItem -LiteralPath (Join-Path $achievementMain ('Assets/_Project/' + $achievementDirectory)) -Recurse -File) {
        if ($achievementFile.Extension -notin @('.cs','.asmdef','.meta')) { continue }
        $achievementRelative = $achievementFile.FullName.Substring($achievementMain.Length + 1)
        $achievementTarget = [IO.Path]::GetFullPath((Join-Path $achievementProject $achievementRelative))
        if (-not $achievementTarget.StartsWith($achievementProject + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe sync path.' }
        if ((Test-Path -LiteralPath $achievementTarget) -and (Get-FileHash -LiteralPath $achievementFile.FullName).Hash -eq (Get-FileHash -LiteralPath $achievementTarget).Hash) { continue }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($achievementTarget)) | Out-Null
        Copy-Item -LiteralPath $achievementFile.FullName -Destination $achievementTarget -Force
        $script:achievementCopied.Add($achievementRelative)
    }
}
$achievementSettings = [regex]::Replace($achievementSettings, '(?m)^  companyName:.*$', '  companyName: CodexAchievementValidation')
$achievementSettings = [regex]::Replace($achievementSettings, '(?m)^  productName:.*$', '  productName: AchievementBalance20261022')
[IO.File]::WriteAllText($achievementSettingsPath, $achievementSettings, [Text.UTF8Encoding]::new($false))
$script:achievementCopied | Set-Content -LiteralPath (Join-Path $achievementOutput 'code-sync.txt') -Encoding utf8
Write-Output ('Prepared isolated achievement project; changed code files: ' + $script:achievementCopied.Count)
