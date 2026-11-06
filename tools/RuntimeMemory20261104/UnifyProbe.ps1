$ErrorActionPreference='Stop'
$task='D:\Game\Artifacts\RuntimeMemory20261104'
$record=Join-Path $task 'canonical-probe'
if(Test-Path -LiteralPath $record){throw 'Preserve existing probe parity evidence.'}
if(@(Get-CimInstance Win32_Process -Filter "Name='MemoryProbe.exe'").Count){throw 'Wait for diagnostic Players to exit.'}
$relative='MemoryProbe_Data\Managed\ShatteredSail.RuntimeMemory20261104.dll'
$baseline=Join-Path $task 'builds\baseline\Windows'
$optimized=Join-Path $task 'builds\optimized\Windows'
$sourcePath='Assets\RuntimeMemory20261104Probe\RuntimeMemory20261104Probe.cs'
$baseInput=@(Import-Csv (Join-Path $task 'validation\baseline-full-inputs.csv') | Where-Object Path -eq $sourcePath)
$optInput=@(Import-Csv (Join-Path $task 'validation\optimized-full-inputs.csv') | Where-Object Path -eq $sourcePath)
if($baseInput.Count -ne 1 -or $optInput.Count -ne 1 -or $baseInput[0].SHA256 -ne $optInput[0].SHA256){throw 'Diagnostic source inputs differ.'}
function Inventory([string]$root){
    if($root -ne $baseline -and $root -ne $optimized){throw 'Unexpected build root.'}
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($root)
    while($pending.Count){
        $directory=$pending.Pop()
        if((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build directory.'}
        foreach($item in Get-ChildItem -LiteralPath $directory -Force){
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build entry.'}
            if($item.PSIsContainer){$pending.Push($item.FullName)}else{
                [pscustomobject]@{Path=$item.FullName.Substring($root.Length+1);Bytes=$item.Length;SHA256=(Get-FileHash -LiteralPath $item.FullName).Hash}
            }
        }
    }
}
New-Item -ItemType Directory -Path $record | Out-Null
$before=@(Inventory $optimized)
$before | Export-Csv -LiteralPath (Join-Path $record 'optimized-before.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath (Join-Path $optimized $relative) -Destination (Join-Path $record 'optimized-original-probe.dll')
Copy-Item -LiteralPath (Join-Path $baseline $relative) -Destination (Join-Path $record 'canonical-probe.dll')
Copy-Item -LiteralPath (Join-Path $baseline $relative) -Destination (Join-Path $optimized $relative) -Force
$after=@(Inventory $optimized)
$after | Export-Csv -LiteralPath (Join-Path $record 'optimized-after.csv') -NoTypeInformation -Encoding utf8
$hashes=@{};foreach($entry in $before){$hashes[$entry.Path]=$entry.SHA256}
$changed=@($after | Where-Object {$hashes[$_.Path] -ne $_.SHA256})
if($before.Count -ne $after.Count -or $changed.Count -ne 1 -or $changed[0].Path -ne $relative){throw 'Only the diagnostic probe DLL may change.'}
$canonical=(Get-FileHash -LiteralPath (Join-Path $baseline $relative)).Hash
if((Get-FileHash -LiteralPath (Join-Path $optimized $relative)).Hash -ne $canonical){throw 'Probe parity failed.'}
[pscustomobject]@{Method='Diagnostic-only DLL postprocess. Both builds used identical probe source; use the exact baseline diagnostic DLL in both trials. No production assembly, resource, setting or executable changed. Original optimized probe and full before/after manifests retained.';ProbeSourceSHA256=$baseInput[0].SHA256;CanonicalProbeSHA256=$canonical;ChangedFiles=$changed;BuildFileCount=$after.Count} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $record 'parity.json') -Encoding utf8
'Probe parity verified; exactly one diagnostic DLL changed, all production build files preserved.'
