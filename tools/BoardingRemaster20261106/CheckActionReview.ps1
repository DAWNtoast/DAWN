$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$tempRoot = (Resolve-Path -LiteralPath (Join-Path $root 'tmp')).Path
if ((Get-Item -LiteralPath $tempRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked temporary root.' }
$profile = Join-Path $tempRoot ('boarding-review-browser-' + [Guid]::NewGuid().ToString('N'))
$chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe'
if (!(Test-Path -LiteralPath $chrome)) { throw 'Local Chrome is unavailable.' }
$null = New-Item -ItemType Directory -Path $profile
try {
    & node (Join-Path $PSScriptRoot 'CheckActionReview.mjs') $chrome $profile
    if ($LASTEXITCODE -ne 0) { throw "Browser checks failed with exit code $LASTEXITCODE" }
} finally {
    $candidate = (Resolve-Path -LiteralPath $profile).Path
    $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
    if ((Split-Path -Path $candidate -Parent) -ne $resolvedTemp -or $candidate -ne $profile) { throw "Unexpected profile path $candidate" }
    $rootItem = Get-Item -LiteralPath $candidate -Force
    if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked profile root.' }
    $all = @(Get-ChildItem -LiteralPath $candidate -Recurse -Force)
    if ($all | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'A link exists in the browser profile.' }
    $users = @(Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($candidate) })
    if ($users.Count) { throw "Browser is still using $candidate" }
    $bytes = [long](($all | Where-Object { !$_.PSIsContainer } | Measure-Object -Property Length -Sum).Sum)
    Remove-Item -LiteralPath $candidate -Recurse -Force
    if (Test-Path -LiteralPath $candidate) { throw "Profile was not removed: $candidate" }
    $log = Join-Path $root 'outputs/boarding-remaster-20261106/validation/review-browser/profile-cleanup.csv'
    $null = [IO.Directory]::CreateDirectory((Split-Path -Path $log -Parent))
    [PSCustomObject]@{ Path=$candidate; Removed=$true; ReclaimedBytes=$bytes } | Export-Csv -LiteralPath $log -Append -NoTypeInformation
    Write-Output "Released $bytes temporary browser-profile bytes."
}
