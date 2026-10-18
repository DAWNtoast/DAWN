param([Parameter(Mandatory=$true)][string]$Folder)
$ErrorActionPreference = 'Stop'
$base = [IO.Path]::GetFullPath('D:/Game/Artifacts/NavalDragRoute20261015')
$run = [IO.Path]::GetFullPath((Join-Path $base $Folder))
if (-not $run.StartsWith($base + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Only this task validation directory is accepted.' }
$reports = @(Get-ChildItem -LiteralPath $run -Filter '*-results.xml' -File)
foreach ($file in $reports) {
    [xml]$report = Get-Content -LiteralPath $file.FullName -Raw
    $report.DocumentElement | Select-Object result,total,passed,failed | ConvertTo-Json -Compress
    $report.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
        [pscustomobject]@{Name=$_.fullname;Message=$_.failure.message.InnerText;Stack=$_.failure.'stack-trace'.InnerText}
    } | ConvertTo-Json -Depth 4
}
if (-not $reports.Count) { Get-ChildItem -LiteralPath $run -Filter '*.log' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Tail 8 } }
