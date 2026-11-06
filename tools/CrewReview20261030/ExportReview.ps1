param([switch]$RefreshInputs)
$ErrorActionPreference='Stop'
$crewRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$crewPython='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
if($RefreshInputs){
    & (Join-Path $PSScriptRoot 'export_data.ps1')
    if($LASTEXITCODE -ne 0){throw 'Crew data export failed'}
    & (Join-Path $PSScriptRoot 'ExportCaptures.ps1')
    if($LASTEXITCODE -ne 0){throw 'Crew capture failed'}
    & (Join-Path $PSScriptRoot 'CaptureThumbnails.ps1')
    if($LASTEXITCODE -ne 0){throw 'Crew screenshot preview generation failed'}
    & (Join-Path $PSScriptRoot 'CaptureArtThumbnails.ps1')
    if($LASTEXITCODE -ne 0){throw 'Crew art preview generation failed'}
}
& $crewPython (Join-Path $PSScriptRoot 'build_review.py')
if($LASTEXITCODE -ne 0){throw 'Crew review build failed'}
& node (Join-Path $PSScriptRoot 'verify_review.cjs')
if($LASTEXITCODE -ne 0){throw 'Crew review validation failed'}
Write-Output (Join-Path $crewRoot 'outputs/crew-review-20261030/全部船员审核.html')
