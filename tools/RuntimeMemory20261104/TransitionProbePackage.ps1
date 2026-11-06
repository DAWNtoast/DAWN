param(
    [Parameter(Mandatory=$true)][ValidateSet('Compile','Install','Restore')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='transition-probe-v2'
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath('D:\Game\Artifacts\RuntimeMemory20261104')
$evidence=Join-Path $taskRoot $EvidenceSet
$source=Join-Path $PSScriptRoot 'RuntimeMemory20261104TransitionProbe.cs'
$assemblyName='ShatteredSail.RuntimeMemory20261104'
$assemblyFile=$assemblyName+'.dll'
$relativeDll='MemoryProbe_Data\Managed\'+$assemblyFile
$buildRoots=@{baseline=(Join-Path $taskRoot 'builds\baseline\Windows');optimized=(Join-Path $taskRoot 'builds\optimized\Windows')}
$newDll=Join-Path $evidence $assemblyFile
function Assert-TaskPath([string]$Path) {
    $resolved=[IO.Path]::GetFullPath($Path)
    if(!$resolved.StartsWith($taskRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw ('Outside task artifacts: '+$resolved)}
    for($at=$resolved;$at -and $at.Length -ge $taskRoot.Length;$at=Split-Path $at -Parent){
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked artifact: '+$at)}
    }
}
function Build-Manifest([string]$Root) {
    Assert-TaskPath $Root
    $files=[Collections.Generic.List[object]]::new();$pending=[Collections.Generic.Stack[string]]::new();$pending.Push($Root)
    while($pending.Count){
        $at=$pending.Pop()
        foreach($item in Get-ChildItem -LiteralPath $at -Force){
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Linked build entry: '+$item.FullName)}
            if($item.PSIsContainer){$pending.Push($item.FullName)}
            else {$files.Add([pscustomobject]@{Path=$item.FullName.Substring($Root.Length+1);Bytes=$item.Length;SHA256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash})}
        }
    }
    return @($files.ToArray()|Sort-Object Path)
}
function Read-Registration([string]$Root) {
    $file=Join-Path $Root 'MemoryProbe_Data\RuntimeInitializeOnLoads.json'
    $data=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
    $entry=@($data.root|Where-Object {$_.assemblyName -eq $assemblyName})
    if($entry.Count -ne 1 -or $entry[0].nameSpace -ne 'ShatteredSail.Diagnostics' -or
        $entry[0].className -ne 'RuntimeMemory20261104Probe' -or $entry[0].methodName -ne 'Bootstrap' -or
        [int]$entry[0].loadTypes -ne 1 -or $entry[0].isUnityClass){throw ('Unexpected RuntimeInitializeOnLoads registration: '+$file)}
    [pscustomobject]@{Path=$file;SHA256=(Get-FileHash -LiteralPath $file).Hash;Entry=$entry[0]}
}
function Read-ProbeMetadata([string]$File) {
    Add-Type -AssemblyName System.Reflection.Metadata
    $stream=[IO.File]::OpenRead($File);$reader=$null
    try {
        $reader=[Reflection.PortableExecutable.PEReader]::new($stream)
        $metadata=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
        $name=$metadata.GetString($metadata.GetAssemblyDefinition().Name)
        $found=[Collections.Generic.List[object]]::new()
        foreach($handle in $metadata.TypeDefinitions){
            $type=$metadata.GetTypeDefinition($handle)
            if($metadata.GetString($type.Name) -ne 'RuntimeMemory20261104Probe' -or $metadata.GetString($type.Namespace) -ne 'ShatteredSail.Diagnostics'){continue}
            foreach($methodHandle in $type.GetMethods()){
                $method=$metadata.GetMethodDefinition($methodHandle)
                if($metadata.GetString($method.Name) -ne 'Bootstrap'){continue}
                $found.Add([pscustomobject]@{Assembly=$name;Namespace=$metadata.GetString($type.Namespace);Type=$metadata.GetString($type.Name);Method='Bootstrap';Static=([bool]($method.Attributes -band [Reflection.MethodAttributes]::Static));ParameterDefinitions=$method.GetParameters().Count;SignatureHex=([BitConverter]::ToString($metadata.GetBlobBytes($method.Signature)))})
            }
        }
        if($name -ne $assemblyName -or $found.Count -ne 1 -or !$found[0].Static -or $found[0].ParameterDefinitions -ne 0 -or $found[0].SignatureHex -ne '00-00-01'){
            throw 'Compiled diagnostic Bootstrap identity/signature does not match the registered static void Bootstrap().' 
        }
        return $found[0]
    } finally {if($reader){$reader.Dispose()};$stream.Dispose()}
}
function Assert-BuildsIdle {
    $active=@(Get-CimInstance Win32_Process|Where-Object {
        if($_.ProcessId -eq $PID){return $false}
        $command=$_.CommandLine
        if(!$command){return $false}
        $command=$command.Replace('/','\')
        return $command.Contains($buildRoots.baseline) -or $command.Contains($buildRoots.optimized) -or $command.Contains('-runtimeMemory20261104Output')
    })
    if($active.Count){throw ('Diagnostic build is in use; preserve it. PIDs: '+($active.ProcessId -join ','))}
}
Assert-TaskPath $evidence
if($Stage -eq 'Compile'){
    if(Test-Path -LiteralPath $evidence){throw 'Preserve previous compiler/installation evidence; use a fresh EvidenceSet.'}
    New-Item -ItemType Directory -Path $evidence|Out-Null
    $registrations=@(foreach($variant in @('baseline','optimized')){Read-Registration $buildRoots[$variant]})
    $registrations|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $evidence 'bootstrap-registrations-before.json') -Encoding utf8
    $managed=Join-Path $buildRoots.baseline 'MemoryProbe_Data\Managed'
    $refs=@(Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File|Where-Object Name -ne $assemblyFile|Sort-Object Name)
    if(!$refs.Count){throw 'Actual baseline Player Managed references are missing.'}
    $refHashes=@($refs|ForEach-Object{[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    $sourceHash=(Get-FileHash -LiteralPath $source).Hash
    Copy-Item -LiteralPath $source -Destination (Join-Path $evidence 'source.cs')
    $args=@('-target:library','-nostdlib+','-langversion:9.0','-deterministic+','-optimize+','-nologo',
        '-define:UNITY_6000_3,UNITY_STANDALONE_WIN,UNITY_STANDALONE')
    $args+=@($refs|ForEach-Object{'-r:"'+$_.FullName.Replace('\','/')+'"'})
    $args+='-out:"'+$newDll.Replace('\','/')+'"'
    $args+='"'+$source.Replace('\','/')+'"'
    $response=Join-Path $evidence 'compile.rsp';[IO.File]::WriteAllLines($response,$args,[Text.UTF8Encoding]::new($false))
    $compiler='D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\DotNetSdkRoslyn\csc.dll'
    & 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\NetCoreRuntime\dotnet.exe' $compiler ('@'+$response) 2>&1|Tee-Object -FilePath (Join-Path $evidence 'compile.log')
    if($LASTEXITCODE -ne 0){throw 'Transition probe compilation failed.'}
    if((Get-FileHash -LiteralPath $source).Hash -ne $sourceHash){throw 'Probe source changed during compile.'}
    foreach($reference in $refHashes){if((Get-FileHash -LiteralPath $reference.Path).Hash -ne $reference.SHA256){throw 'Player dependency changed during compile.'}}
    $metadata=Read-ProbeMetadata $newDll
    [pscustomobject]@{Result='PASS';Source=$source;SourceSHA256=$sourceHash;ProbeDll=$newDll;ProbeDllSHA256=(Get-FileHash -LiteralPath $newDll).Hash;Compiler=$compiler;CompilerSHA256=(Get-FileHash -LiteralPath $compiler).Hash;References=$refHashes;Bootstrap=$metadata;Installed=$false;RuntimeCalibration='Still required; static registration/metadata verification is not a successful Player run.'}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $evidence 'compile-verification.json') -Encoding utf8
    Write-Output ('COMPILE PASS; not installed: '+$newDll)
    exit
}
Assert-BuildsIdle
$compile=Get-Content -LiteralPath (Join-Path $evidence 'compile-verification.json') -Raw|ConvertFrom-Json
if($compile.Result -ne 'PASS' -or (Get-FileHash -LiteralPath $newDll).Hash -ne $compile.ProbeDllSHA256){throw 'Compiled diagnostic evidence/hash mismatch.'}
[void](Read-ProbeMetadata $newDll)
if($Stage -eq 'Install'){
    if(Test-Path -LiteralPath (Join-Path $evidence 'install-verification.json')){throw 'Preserve prior installation evidence.'}
    $before=@{};$oldHashes=@{}
    foreach($variant in @('baseline','optimized')){
        [void](Read-Registration $buildRoots[$variant])
        $before[$variant]=Build-Manifest $buildRoots[$variant]
        $before[$variant]|Export-Csv -LiteralPath (Join-Path $evidence ($variant+'-before.csv')) -NoTypeInformation -Encoding utf8
        $current=Join-Path $buildRoots[$variant] $relativeDll
        $oldHashes[$variant]=(Get-FileHash -LiteralPath $current).Hash
        $backup=Join-Path $evidence ($variant+'-previous-canonical.dll')
        if(Test-Path -LiteralPath $backup){throw 'Preserve an existing canonical backup.'}
        Copy-Item -LiteralPath $current -Destination $backup
    }
    if($oldHashes.baseline -ne $oldHashes.optimized){throw 'Both prior diagnostic DLLs must already be the same canonical version.'}
    $changedVariants=[Collections.Generic.List[string]]::new()
    try {
        foreach($variant in @('baseline','optimized')){
            Copy-Item -LiteralPath $newDll -Destination (Join-Path $buildRoots[$variant] $relativeDll) -Force
            $changedVariants.Add($variant)
            $after=Build-Manifest $buildRoots[$variant]
            $after|Export-Csv -LiteralPath (Join-Path $evidence ($variant+'-after.csv')) -NoTypeInformation -Encoding utf8
            $diff=@(Compare-Object $before[$variant] $after -Property Path,Bytes,SHA256)
            if($before[$variant].Count -ne $after.Count -or @($diff|Where-Object Path -ne $relativeDll).Count -or
                @($diff|Where-Object Path -eq $relativeDll).Count -ne 2){throw ('Unexpected full-build difference for '+$variant)}
            [void](Read-Registration $buildRoots[$variant])
        }
    } catch {
        foreach($variant in $changedVariants){Copy-Item -LiteralPath (Join-Path $evidence ($variant+'-previous-canonical.dll')) -Destination (Join-Path $buildRoots[$variant] $relativeDll) -Force}
        throw
    }
    [pscustomobject]@{Result='PASS';Scope='Diagnostic-only transition-v2 probe postprocess; exactly one DLL per package changed. Production assemblies/resources/executables/settings/RuntimeInitializeOnLoads registrations unchanged.';PreviousCanonicalSHA256=$oldHashes.baseline;NewCanonicalSHA256=$compile.ProbeDllSHA256;ChangedRelativePath=$relativeDll;BaselineFiles=$before.baseline.Count;OptimizedFiles=$before.optimized.Count;RuntimeCalibration='Required before accepting measurement.';InstalledUtc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $evidence 'install-verification.json') -Encoding utf8
    Write-Output 'INSTALL PASS; same v2 probe in both diagnostic packages. Run calibration before trials.'
    exit
}
$install=Get-Content -LiteralPath (Join-Path $evidence 'install-verification.json') -Raw|ConvertFrom-Json
foreach($variant in @('baseline','optimized')){
    $current=Join-Path $buildRoots[$variant] $relativeDll
    if((Get-FileHash -LiteralPath $current).Hash -ne $install.NewCanonicalSHA256){throw 'Current probe differs; do not overwrite another change.'}
    $backup=Join-Path $evidence ($variant+'-previous-canonical.dll')
    if((Get-FileHash -LiteralPath $backup).Hash -ne $install.PreviousCanonicalSHA256){throw 'Canonical backup mismatch.'}
    Copy-Item -LiteralPath $backup -Destination $current -Force
    $restored=Build-Manifest $buildRoots[$variant]
    $restored|Export-Csv -LiteralPath (Join-Path $evidence ($variant+'-restored.csv')) -NoTypeInformation -Encoding utf8
    if(@(Compare-Object (Import-Csv -LiteralPath (Join-Path $evidence ($variant+'-before.csv'))) $restored -Property Path,Bytes,SHA256).Count){throw ('Restored build differs from original: '+$variant)}
}
[pscustomobject]@{Result='PASS';RestoredUtc=[DateTime]::UtcNow.ToString('O');ProbeSHA256=$install.PreviousCanonicalSHA256;Scope='Both complete diagnostic packages match their original pre-v2 manifests.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $evidence 'restore-verification.json') -Encoding utf8
Write-Output 'RESTORE PASS; original canonical probe and full manifests restored.'
