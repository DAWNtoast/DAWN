$ErrorActionPreference = 'Stop'
$project = 'D:/Game/UnityClient'
$conflicts = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\', '/').IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
if ($conflicts.Count -gt 0) { throw 'Finish this project Unity run before removing the temporary Editor tool.' }
$source = Join-Path $PSScriptRoot 'HeaderRedesignCapture20261014.cs'
$installed = [IO.Path]::GetFullPath('D:/Game/UnityClient/Assets/_Project/Editor/HeaderRedesignCapture20261014.cs')
$editorRoot = [IO.Path]::GetFullPath('D:/Game/UnityClient/Assets/_Project/Editor') + [IO.Path]::DirectorySeparatorChar
if (-not $installed.StartsWith($editorRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup target.' }
if (Test-Path -LiteralPath $installed) {
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) { throw 'Installed source was changed; preserve it instead of removing it.' }
    Remove-Item -LiteralPath $installed
    $meta = $installed + '.meta'
    if (Test-Path -LiteralPath $meta) { Remove-Item -LiteralPath $meta }
    Write-Output 'Removed the matching temporary Editor capture source and its import metadata.'
}