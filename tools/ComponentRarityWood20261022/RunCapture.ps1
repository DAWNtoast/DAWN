param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
$captureProject = 'D:\Game\tmp\component-rarity-wood-20261022'
$captureOutput = [IO.Path]::GetFullPath($Output)
if (-not $captureOutput.StartsWith('D:\Game\Artifacts\ComponentRarityWood20261022\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected output directory.' }
if (Test-Path -LiteralPath $captureOutput) { throw 'Use a new output directory.' }
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($captureProject) }).Count) { throw 'Validation project occupied.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $captureProject 'Assets/_Project/Editor/ComponentRarityWoodCapture.cs') -Force
[IO.Directory]::CreateDirectory($captureOutput) | Out-Null
$captureArguments = @('-batchmode','-force-d3d11','-projectPath',$captureProject,'-quit','-executeMethod','ShatteredSail.Editor.ComponentRarityWoodCapture.Run','-rarityWoodOutput',(Join-Path $captureOutput 'images'),'-logFile',(Join-Path $captureOutput 'unity.log'))
$quoted = $captureArguments | ForEach-Object { '"' + $_ + '"' }
$captureProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $quoted -WindowStyle Hidden -PassThru
$null = $captureProcess.Handle
Write-Output ('Capture started: ' + $captureProcess.Id)
while (-not $captureProcess.WaitForExit(15000)) { Write-Output 'Capture running...' }
if ($captureProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $captureOutput 'images/capture-complete.txt'))) { throw 'Capture failed; inspect unity.log.' }
Write-Output ('Capture complete: ' + $captureOutput)
