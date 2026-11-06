param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateRange(30,120)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath('D:\Game\Artifacts\RuntimeMemory20261104').TrimEnd('\') + '\'
$probeExe = [IO.Path]::GetFullPath($Executable)
$trialOutput = [IO.Path]::GetFullPath($OutputDirectory)
function Assert-TaskPath([string]$Path) {
    if (!$Path.StartsWith($taskRoot,[StringComparison]::OrdinalIgnoreCase)) { throw ('Outside task artifacts: '+$Path) }
    for ($at=$Path; $at -and $at.Length -ge $taskRoot.TrimEnd('\').Length; $at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked task path: '+$at) }
    }
}
function Quote-Arguments([string[]]$Values) {
    (($Values | ForEach-Object {
        $value=[regex]::Replace($_,'(\\*)"','$1$1\"')
        '"'+[regex]::Replace($value,'(\\+)$','$1$1')+'"'
    }) -join ' ')
}
function Median($Values) {
    $sorted=@($Values | ForEach-Object {[double]$_} | Sort-Object)
    if (!$sorted.Count) { throw 'Cannot compute a median without samples.' }
    $middle=[int][Math]::Floor($sorted.Count/2)
    if($sorted.Count%2){return $sorted[$middle]}
    return ($sorted[$middle-1]+$sorted[$middle])/2
}
function As-UtcInstant([object]$Value) {
    # Newer PowerShell can deserialize ISO JSON timestamps into DateTime automatically.
    # Never pass that DateTime through Parse(string): its implicit text loses the UTC Kind.
    if($Value -is [DateTimeOffset]){return $Value.ToUniversalTime()}
    if($Value -is [DateTime]){
        if($Value.Kind -eq [DateTimeKind]::Unspecified){throw 'Timestamp lost its timezone; require an explicit UTC/local Kind.'}
        return [DateTimeOffset]::new($Value).ToUniversalTime()
    }
    return [DateTimeOffset]::Parse([string]$Value,[Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal)
}
function New-GpuCounterState {
    $state=@{Available=$false;Category=$null;Counters=@{};NextDiscovery=[DateTime]::MinValue;Errors=[Collections.Generic.HashSet[string]]::new()}
    try {
        Add-Type -AssemblyName System.Diagnostics.PerformanceCounter
        if(![Diagnostics.PerformanceCounterCategory]::Exists('GPU Process Memory')){throw 'GPU Process Memory category is unavailable.'}
        $state.Category=[Diagnostics.PerformanceCounterCategory]::new('GPU Process Memory')
        $state.Available=$true
    } catch {[void]$state.Errors.Add($_.Exception.Message)}
    return $state
}
function Read-GpuProcessMemory($State,[int]$ProcessId) {
    $sample=[pscustomobject]@{Valid=$false;DedicatedBytes=$null;SharedBytes=$null;Instances=0}
    if(!$State.Available){return $sample}
    try {
        if([DateTime]::UtcNow -ge $State.NextDiscovery){
            # Instance discovery is bounded to once per second; the gauge reads below never wait for a rate sample.
            $pattern='^pid_'+$ProcessId+'_'
            $names=@($State.Category.GetInstanceNames()|Where-Object {$_ -match $pattern})
            foreach($name in @($State.Counters.Keys)){
                if($name -notin $names){$State.Counters[$name].Dedicated.Dispose();$State.Counters[$name].Shared.Dispose();$State.Counters.Remove($name)}
            }
            foreach($name in $names){
                if($State.Counters.ContainsKey($name)){continue}
                $dedicated=$null;$shared=$null
                try {
                    $dedicated=[Diagnostics.PerformanceCounter]::new('GPU Process Memory','Dedicated Usage',$name,$true)
                    $shared=[Diagnostics.PerformanceCounter]::new('GPU Process Memory','Shared Usage',$name,$true)
                    if($dedicated.CounterType -ne [Diagnostics.PerformanceCounterType]::NumberOfItems64 -or
                        $shared.CounterType -ne [Diagnostics.PerformanceCounterType]::NumberOfItems64){throw 'Expected GPU byte gauges (NumberOfItems64).'}
                    $State.Counters[$name]=@{Dedicated=$dedicated;Shared=$shared}
                } catch {
                    if($dedicated){$dedicated.Dispose()};if($shared){$shared.Dispose()};throw
                }
            }
            $State.NextDiscovery=[DateTime]::UtcNow.AddSeconds(1)
        }
        if(!$State.Counters.Count){return $sample}
        $dedicatedBytes=0L;$sharedBytes=0L
        foreach($counter in $State.Counters.Values){
            # These are instantaneous Windows process GPU-memory byte gauges, not CPU PrivateBytes or rate counters.
            $d=[long]$counter.Dedicated.RawValue;$s=[long]$counter.Shared.RawValue
            if($d -lt 0 -or $s -lt 0){throw 'Negative GPU byte gauge.'}
            $dedicatedBytes+=$d;$sharedBytes+=$s
        }
        $sample.Valid=$true;$sample.DedicatedBytes=$dedicatedBytes;$sample.SharedBytes=$sharedBytes;$sample.Instances=$State.Counters.Count
    } catch {
        [void]$State.Errors.Add($_.Exception.Message)
        $State.NextDiscovery=[DateTime]::MinValue
    }
    return $sample
}
function Close-GpuCounters($State) {
    if(!$State){return}
    foreach($counter in $State.Counters.Values){$counter.Dedicated.Dispose();$counter.Shared.Dispose()}
    $State.Counters.Clear()
}
function Stop-OwnedProbe {
    if(!$probeProcess){return $false}
    $probeProcess.Refresh()
    if($probeProcess.HasExited){return $false}
    $current=Get-CimInstance Win32_Process -Filter ('ProcessId='+$probeProcess.Id)
    if($current -and $current.ExecutablePath -and
        [string]::Equals([IO.Path]::GetFullPath($current.ExecutablePath),$probeExe,[StringComparison]::OrdinalIgnoreCase) -and
        $current.CommandLine -and $current.CommandLine.Contains($probePath) -and
        $probeProcess.StartTime.ToUniversalTime().Ticks -eq $ownedStartTicks) {
        Stop-Process -Id $probeProcess.Id -Force
        return $true
    }
    return $false
}
Assert-TaskPath $probeExe
Assert-TaskPath $trialOutput
if (!(Test-Path -LiteralPath $probeExe -PathType Leaf) -or [IO.Path]::GetExtension($probeExe) -ne '.exe') { throw 'Use an existing diagnostic .exe.' }
if (Test-Path -LiteralPath $trialOutput) { throw 'Preserve prior evidence; OutputDirectory must be new.' }
$managed=Join-Path ([IO.Path]::GetDirectoryName($probeExe)) ([IO.Path]::GetFileNameWithoutExtension($probeExe)+'_Data\Managed')
$probeDll=Join-Path $managed 'ShatteredSail.RuntimeMemory20261104.dll'
Assert-TaskPath $probeDll
if (!(Test-Path -LiteralPath $probeDll -PathType Leaf)) { throw 'The diagnostic probe DLL is missing.' }
$occupied=@(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -or ($_.CommandLine -and $_.CommandLine.Contains('-runtimeMemory20261104Output'))
})
if($occupied.Count){throw ('Use an exclusive Unity/diagnostic Player measurement slot; active PIDs: '+($occupied.ProcessId -join ','))}
New-Item -ItemType Directory -Path $trialOutput | Out-Null
$probePath=Join-Path $trialOutput 'probe.json'
$arguments=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
    '-runtimeMemory20261104Output',$probePath,'-logFile',(Join-Path $trialOutput 'player.log'))
$exeHash=(Get-FileHash -LiteralPath $probeExe -Algorithm SHA256).Hash
$probeHash=(Get-FileHash -LiteralPath $probeDll -Algorithm SHA256).Hash
$assemblies=@(Get-ChildItem -LiteralPath $managed -Filter 'ShatteredSail.*.dll' -File | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{Name=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$samples=[Collections.Generic.List[object]]::new()
$validationErrors=[Collections.Generic.List[string]]::new()
$points=[Collections.Generic.List[object]]::new()
$probeProcess=$null
$ownedStartTicks=0L
$timedOut=$false
$ownedProcessStopped=$false
$processExitCode=$null
$gpuState=New-GpuCounterState
$startedUtc=[DateTime]::UtcNow
$timer=[Diagnostics.Stopwatch]::StartNew()
try {
    $probeProcess=Start-Process -FilePath $probeExe -ArgumentList (Quote-Arguments $arguments) -WindowStyle Hidden -PassThru
    $ownedStartTicks=$probeProcess.StartTime.ToUniversalTime().Ticks
    [pscustomobject]@{
        Executable=$probeExe;ExecutableSHA256=$exeHash;ProbeDll=$probeDll;ProbeDllSHA256=$probeHash;Assemblies=$assemblies
        PID=$probeProcess.Id;ProcessStartTicksUtc=$ownedStartTicks;StartedUtc=$startedUtc.ToString('O');Arguments=$arguments
        Window='Hidden; common 1920x1080 SDR offscreen production graphics';SampleIntervalMs=250
        GpuCounterCategory='GPU Process Memory';GpuCounterAvailable=$gpuState.Available
        GpuCounterMethod='Dedicated Usage / Shared Usage NumberOfItems64 raw byte gauges, summed over this PID adapter instances; no Get-Counter delay.'
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $trialOutput 'launch.json') -Encoding utf8
    Write-Output ('MEMORY_PLAYER_PID='+$probeProcess.Id)
    while($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $probeProcess.Refresh()
        if($probeProcess.HasExited){break}
        try {
            $sampleUtc=[DateTime]::UtcNow.ToString('O')
            $gpu=Read-GpuProcessMemory $gpuState $probeProcess.Id
            $samples.Add([pscustomobject]@{
                Utc=$sampleUtc;ElapsedSeconds=[Math]::Round($timer.Elapsed.TotalSeconds,4)
                PrivateBytes=$probeProcess.PrivateMemorySize64;WorkingSetBytes=$probeProcess.WorkingSet64
                GpuValid=$gpu.Valid;GpuDedicatedBytes=$gpu.DedicatedBytes;GpuSharedBytes=$gpu.SharedBytes;GpuInstanceCount=$gpu.Instances
            })
        } catch {
            $probeProcess.Refresh()
            if(!$probeProcess.HasExited){throw}
        }
        Start-Sleep -Milliseconds 250
    }
    $probeProcess.Refresh()
    $timedOut=!$probeProcess.HasExited
    if($timedOut) {
        $validationErrors.Add('Diagnostic Player exceeded the '+$TimeoutSeconds+' second timeout.')
        # Stop only this exact owned launch. A reused PID or mismatched executable is never terminated.
        $ownedProcessStopped=Stop-OwnedProbe
        if(!$ownedProcessStopped){$validationErrors.Add('Timeout process identity could not be verified; preserved the process for inspection.')}
    }
    if($probeProcess.WaitForExit(5000)){$processExitCode=$probeProcess.ExitCode}
    else {$validationErrors.Add('Owned Player did not exit after the bounded wait.')}
    if($processExitCode -ne 0){$validationErrors.Add('Player exit code: '+$processExitCode)}
    if((Get-FileHash -LiteralPath $probeExe).Hash -ne $exeHash -or (Get-FileHash -LiteralPath $probeDll).Hash -ne $probeHash){
        $validationErrors.Add('Executable or probe DLL changed during measurement.')
    }
    if(!(Test-Path -LiteralPath $probePath -PathType Leaf)) { $validationErrors.Add('Probe JSON is missing.') }
    else {
        try {
            $probe=Get-Content -LiteralPath $probePath -Raw | ConvertFrom-Json
            if($probe.probeVersion -ne 'transition-v2' -or !$probe.success -or !$probe.identityVerified -or $probe.developmentBuild -or
                $probe.company -ne 'ShatteredSailAudit' -or $probe.product -ne 'RuntimeMemory20261104') {
                $validationErrors.Add('Probe success/non-Development/isolated identity validation failed: '+$probe.error)
            }
            if($probe.graphicsApi -eq 'Null' -or [int]$probe.renderedFrames -lt 100){$validationErrors.Add('Insufficient actual graphical rendering.')}
            if(@($probe.errors).Count){$validationErrors.Add('Probe logged '+@($probe.errors).Count+' errors.')}
            $expected=@('menu_initial','art_open_first','art_live_after_collection','art_closed_first','art_reopened','art_closed_second','ram_open','ram_closed')
            if(@($probe.points).Count -ne $expected.Count){$validationErrors.Add('Expected exactly eight completed transition-v2 measurement windows.')}
            foreach($name in $expected) {
                $matches=@($probe.points | Where-Object name -eq $name)
                if($matches.Count -ne 1){$validationErrors.Add('Missing/duplicate measurement window: '+$name);continue}
                $point=$matches[0]
                $begin=As-UtcInstant $point.windowStartUtc
                $end=As-UtcInstant $point.windowEndUtc
                if($end -le $begin){$validationErrors.Add('Invalid UTC window: '+$name);continue}
                $window=@($samples | Where-Object { $at=As-UtcInstant $_.Utc;$at -ge $begin -and $at -le $end })
                if($window.Count -lt 4){$validationErrors.Add('Fewer than four OS samples in '+$name)}
                if([int]$point.framesInWindow -lt 30){$validationErrors.Add('Fewer than 30 actual rendered frames in '+$name)}
                if($name -in @('art_open_first','art_live_after_collection','art_reopened') -and
                    ([int]$point.targetTextureCount -ne 15 -or [int]$point.targetSpriteCount -ne 5)){
                    $validationErrors.Add('Visible target resources were not intact at '+$name)
                }
                if($name -eq 'ram_open' -and [int]$point.ramTextureCount -ne 4){$validationErrors.Add('Follow-on production ram sequence must retain four pages.')}
                if($name -ne 'ram_open' -and [int]$point.ramTextureCount -ne 0){$validationErrors.Add('Unexpected ram texture retained at '+$name)}
                if(!$window.Count){continue}
                $gpuWindow=@($window|Where-Object {$_.GpuValid -and $null -ne $_.GpuDedicatedBytes -and $null -ne $_.GpuSharedBytes})
                $points.Add([pscustomobject]@{
                    Name=$name;WindowStartUtc=$begin.ToString('O');WindowEndUtc=$end.ToString('O');Samples=$window.Count;RenderedFrames=[int]$point.framesInWindow
                    PrivateBytesMedian=(Median $window.PrivateBytes);PrivateBytesMinimum=($window|Measure-Object PrivateBytes -Minimum).Minimum;PrivateBytesMaximum=($window|Measure-Object PrivateBytes -Maximum).Maximum
                    WorkingSetBytesMedian=(Median $window.WorkingSetBytes);WorkingSetBytesMinimum=($window|Measure-Object WorkingSetBytes -Minimum).Minimum;WorkingSetBytesMaximum=($window|Measure-Object WorkingSetBytes -Maximum).Maximum
                    GpuValidSamples=$gpuWindow.Count;GpuMedianAvailable=($gpuWindow.Count -ge 4)
                    GpuDedicatedBytesMedian=if($gpuWindow.Count -ge 4){Median $gpuWindow.GpuDedicatedBytes}else{$null}
                    GpuSharedBytesMedian=if($gpuWindow.Count -ge 4){Median $gpuWindow.GpuSharedBytes}else{$null}
                    GpuDedicatedBytesMinimum=if($gpuWindow.Count){($gpuWindow|Measure-Object GpuDedicatedBytes -Minimum).Minimum}else{$null}
                    GpuDedicatedBytesMaximum=if($gpuWindow.Count){($gpuWindow|Measure-Object GpuDedicatedBytes -Maximum).Maximum}else{$null}
                    GpuSharedBytesMinimum=if($gpuWindow.Count){($gpuWindow|Measure-Object GpuSharedBytes -Minimum).Minimum}else{$null}
                    GpuSharedBytesMaximum=if($gpuWindow.Count){($gpuWindow|Measure-Object GpuSharedBytes -Maximum).Maximum}else{$null}
                    TargetTextureCount=[int]$point.targetTextureCount;TargetSpriteCount=[int]$point.targetSpriteCount
                    RamTextureCount=[int]$point.ramTextureCount;RamTextureNames=@($point.ramTextures|ForEach-Object name)
                    TargetTextureNames=@($point.textures | ForEach-Object name);RuntimeTextureByteApiAvailable=[bool]$point.runtimeTextureByteApiAvailable
                    TargetTextureBytes=[long]$point.targetTextureBytes;UnityAllocatedBytes=[long]$point.totalAllocatedBytes;UnityReservedBytes=[long]$point.totalReservedBytes
                })
            }
        } catch { $validationErrors.Add('Probe analysis failed: '+$_.Exception.Message) }
    }
} catch {
    $validationErrors.Add($_.Exception.ToString())
    try {
        $ownedProcessStopped=Stop-OwnedProbe
        if($ownedProcessStopped -and $probeProcess.WaitForExit(5000)){$processExitCode=$probeProcess.ExitCode}
    } catch {$validationErrors.Add('Owned-process cleanup could not be verified: '+$_.Exception.Message)}
}
finally {
    Close-GpuCounters $gpuState
    $samples | Export-Csv -LiteralPath (Join-Path $trialOutput 'process-memory.csv') -NoTypeInformation -Encoding utf8
    $summary=[pscustomobject]@{
        Result=if($validationErrors.Count){'FAIL'}else{'PASS'};ProbeVersion='transition-v2';Executable=$probeExe;ExecutableSHA256=$exeHash;ProbeDllSHA256=$probeHash
        StartedUtc=$startedUtc.ToString('O');CompletedUtc=[DateTime]::UtcNow.ToString('O');ElapsedSeconds=$timer.Elapsed.TotalSeconds
        PID=if($probeProcess){$probeProcess.Id}else{$null};ExitCode=$processExitCode;TimedOut=$timedOut;OwnedProcessStopped=$ownedProcessStopped
        SampleIntervalMs=250;SampleCount=$samples.Count;Windows=@($points.ToArray());ValidationErrors=@($validationErrors.ToArray())
        GpuCounterAvailable=$gpuState.Available;GpuCounterErrors=@($gpuState.Errors)
        GpuMeasurementScope='Windows GPU Process Memory Dedicated Usage / Shared Usage instantaneous byte gauges, summed across matching PID adapter instances. Distinct from CPU PrivateBytes/WorkingSet; missing/not-ready samples are null, not zero. No CPU+GPU additive total.'
        AllProcessPeakPrivateBytes=($samples|Measure-Object PrivateBytes -Maximum).Maximum
        AllProcessPeakWorkingSetBytes=($samples|Measure-Object WorkingSetBytes -Maximum).Maximum
        PeakScope='Whole diagnostic process, including inventory/PNG capture overhead; prefer stable UTC windows for A/B.'
        MeasurementScope='Transition-v2 synthetic resource lifecycle fixture followed by one complete existing production ram-comic sequence (four pages). Hidden Player with controlled offscreen rendering. CPU PrivateBytes, CPU WorkingSet, Windows process GPU dedicated bytes and GPU shared bytes are separate counters and never added. No benefit is assumed.'
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $trialOutput 'trial-summary.json') -Encoding utf8
    if($probeProcess){$probeProcess.Dispose()}
}
if($validationErrors.Count){throw ('Diagnostic trial failed: '+($validationErrors -join ' | '))}
Write-Output ('PASS '+$trialOutput+'; '+$samples.Count+' OS samples, '+$points.Count+' validated windows.')
