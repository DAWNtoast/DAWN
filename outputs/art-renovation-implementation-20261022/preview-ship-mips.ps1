$ErrorActionPreference = 'Stop'
$mipProject = 'D:\Game\tmp\shipyard-detail-clean-20261011'
$mipPath = Join-Path $mipProject 'Assets/_Project/Resources/NavalTactics/Ships'
if (@(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($mipProject.Replace('\','/')) }).Count) { throw 'Isolated Unity project occupied.' }
$mipFiles = @(Get-ChildItem -LiteralPath $mipPath -File -Filter '*_v2.png.meta')
foreach ($mipFile in $mipFiles) {
    $mipText = [IO.File]::ReadAllText($mipFile.FullName)
    $mipText = [regex]::Replace($mipText, '(?m)^    enableMipMap: 0', '    enableMipMap: 1')
    [IO.File]::WriteAllText($mipFile.FullName, $mipText, [Text.UTF8Encoding]::new($false))
}
Write-Output ('Enabled mipmaps for ' + $mipFiles.Count + ' isolated ship imports. Production source assets unchanged.')
