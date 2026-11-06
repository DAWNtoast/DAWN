param(
    [Parameter(Mandatory=$true)][string]$ScopeFile,
    [Parameter(Mandatory=$true)][string]$SnapshotDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$runRoot=Join-Path $workspace 'Artifacts\IntegratedRelease20261105'
$scopePath=[IO.Path]::GetFullPath($ScopeFile)
$snapshotDirectory=[IO.Path]::GetFullPath($SnapshotDirectory).TrimEnd('\','/')
$evidence=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')
function Assert-UnlinkedChild([string]$Path,[string]$Boundary) {
    $full=[IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $limit=[IO.Path]::GetFullPath($Boundary).TrimEnd('\','/')
    if(!$full.StartsWith($limit+'\',[StringComparison]::OrdinalIgnoreCase)){throw ('Path is not inside the required boundary: '+$full)}
    for($at=$full;$at;$at=Split-Path $at -Parent){
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked verification path: '+$at)}
    }
}
function Read-Record([string]$Path) {
    Assert-UnlinkedChild $Path $runRoot
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){throw ('Required verification evidence is missing: '+$Path)}
    return Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json
}
function Argument-Value($Arguments,[string]$Name) {
    $positions=@(for($index=0;$index -lt $Arguments.Count;$index++){if($Arguments[$index] -ceq $Name){$index}})
    if($positions.Count -ne 1 -or $positions[0]+1 -ge $Arguments.Count){throw ('Missing or duplicate test launch argument: '+$Name)}
    return [string]$Arguments[$positions[0]+1]
}
function Matches-Selector($Case,[string]$Selector) {
    return [string]$Case.classname -ceq $Selector -or (([string]$Case.classname+'.'+[string]$Case.methodname) -ceq $Selector)
}
Assert-UnlinkedChild $scopePath $workspace
Assert-UnlinkedChild $snapshotDirectory $runRoot
Assert-UnlinkedChild $evidence $runRoot
if($evidence -eq $snapshotDirectory -or $evidence.StartsWith($snapshotDirectory+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Use a separate verification output directory.'}
foreach($name in @('changed-scope.json','verification-cases.csv','selection-coverage.csv','skipped-tests.json','verification-summary.json')){
    if(Test-Path -LiteralPath (Join-Path $evidence $name)){throw 'Preserve existing verification evidence; select a new output directory.'}
}
$scopeHash=(Get-FileHash -LiteralPath $scopePath).Hash
$scope=Get-Content -LiteralPath $scopePath -Raw|ConvertFrom-Json
if($scope.SchemaVersion -ne 1 -or $scope.Scope -cne 'ChangedAndRelated' -or $scope.AuthorizationMode -cne 'UserRequestedTargetedOnly' -or [string]::IsNullOrWhiteSpace([string]$scope.UserAuthorization)){
    throw 'This verifier requires the explicit current user-authorized ChangedAndRelated scope.'
}
$snapshot=Read-Record (Join-Path $snapshotDirectory 'snapshot.json')
$sourceManifest=Join-Path $snapshotDirectory 'source-manifest.csv'
$projectManifest=Join-Path $snapshotDirectory 'project-input-manifest.csv'
foreach($path in @($sourceManifest,$projectManifest)){Assert-UnlinkedChild $path $runRoot}
$sourceHash=(Get-FileHash -LiteralPath $sourceManifest).Hash
$projectHash=(Get-FileHash -LiteralPath $projectManifest).Hash
if($snapshot.Result -ne 'PASS' -or $snapshot.SourceManifestSHA256 -ne $sourceHash -or $snapshot.ProjectInputManifestSHA256 -ne $projectHash -or
    $scope.SourceManifestSHA256 -ne $sourceHash -or $scope.ProjectInputManifestSHA256 -ne $projectHash){throw 'Scope or snapshot source/project manifests do not identify the same final inputs.'}
$project=[IO.Path]::GetFullPath([string]$snapshot.Project).TrimEnd('\','/')
Assert-UnlinkedChild $project (Join-Path $workspace 'tmp')
$declaredRuns=@($scope.Runs)
if(!$declaredRuns.Count){throw 'The changed scope must declare its test runs.'}
$seenRuns=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$platforms=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$cases=[Collections.Generic.List[object]]::new()
$coverage=[Collections.Generic.List[object]]::new()
$suites=[Collections.Generic.List[object]]::new()
$inputFiles=[Collections.Generic.List[object]]::new()
foreach($declared in $declaredRuns) {
    $platform=[string]$declared.Platform
    if($platform -cnotin @('EditMode','PlayMode')){throw 'Only explicit EditMode and PlayMode evidence is accepted.'}
    [void]$platforms.Add($platform)
    if([string]::IsNullOrWhiteSpace([string]$declared.RunDirectory)){throw 'A test run directory is required.'}
    $directory=[IO.Path]::GetFullPath([string]$declared.RunDirectory).TrimEnd('\','/')
    Assert-UnlinkedChild $directory $runRoot
    if(!$seenRuns.Add($directory)){throw 'Do not count the same test run more than once.'}
    $selectors=@($declared.Selectors)
    $selectorSet=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if(!$selectors.Count){throw 'Every changed-scope run requires exact fixture or method selectors.'}
    foreach($selector in $selectors) {
        if($selector -isnot [string] -or $selector -notmatch '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)+$' -or !$selectorSet.Add($selector)){
            throw 'Selectors must be unique fully qualified fixture or method names without wildcards, regex, parameters, or namespaces alone.'
        }
    }
    $expectedFilter=$selectors -join ';'
    $launchPath=Join-Path $directory 'launch.json'
    $completionPath=Join-Path $directory 'completion.json'
    $resultsPath=Join-Path $directory 'results.xml'
    $launch=Read-Record $launchPath
    $completion=Read-Record $completionPath
    Assert-UnlinkedChild $resultsPath $runRoot
    if($completion.ExitCode -ne 0){throw ('The targeted test process did not complete successfully: '+$directory)}
    $arguments=@($launch.Arguments)
    if(@($arguments|Where-Object{$_ -ceq '-runTests'}).Count -ne 1 -or
        (Argument-Value $arguments '-testPlatform') -cne $platform -or
        [IO.Path]::GetFullPath((Argument-Value $arguments '-projectPath')).TrimEnd('\','/') -ne $project){throw 'The test launch does not match the final snapshot project and platform.'}
    $actualFilter=Argument-Value $arguments '-testFilter'
    if([string]::IsNullOrWhiteSpace($actualFilter) -or $actualFilter -cne $expectedFilter){throw 'The actual test filter differs from the declared changed-scope selectors; full runs are not accepted here.'}
    if([string]::IsNullOrWhiteSpace([string]$launch.SourceManifest) -or [IO.Path]::GetFullPath([string]$launch.SourceManifest) -ne $sourceManifest){throw 'The launch did not use this exact release source manifest.'}
    foreach($record in @($launch,$completion)){
        if($record.InputValidationResult -ne 'PASS' -or $record.SourceManifestSHA256 -ne $sourceHash -or $record.ProjectInputManifestSHA256 -ne $projectHash){throw 'Test inputs were not verified against this snapshot before and after execution.'}
    }
    $resultsHash=(Get-FileHash -LiteralPath $resultsPath).Hash
    if($completion.ResultsSHA256 -ne $resultsHash){throw 'Test results changed after recorded completion.'}
    foreach($path in @($launchPath,$completionPath,$resultsPath)){$inputFiles.Add([pscustomobject]@{Path=$path;SHA256=(Get-FileHash -LiteralPath $path).Hash})}
    [xml]$xml=Get-Content -LiteralPath $resultsPath -Raw
    $run=$xml.'test-run'
    $testCases=@($xml.SelectNodes('//test-case'))
    if($null -eq $run -or $run.result -notin @('Passed','Skipped:Ignored') -or [int]$run.failed -ne 0 -or [int]$run.inconclusive -ne 0 -or
        [int]$run.passed -le 0 -or [int]$run.skipped -lt 0 -or [int]$run.total -ne ([int]$run.passed+[int]$run.skipped) -or
        $testCases.Count -ne [int]$run.total -or @($testCases|Where-Object result -eq 'Passed').Count -ne [int]$run.passed -or
        @($testCases|Where-Object result -eq 'Skipped').Count -ne [int]$run.skipped -or $xml.SelectNodes('//test-case[@result!="Passed" and @result!="Skipped"]').Count -gt 0){throw 'The targeted result XML has unresolved, failed, empty, or inconsistent cases.'}
    foreach($selector in $selectors) {
        $matching=@($testCases|Where-Object{Matches-Selector $_ $selector})
        $passed=@($matching|Where-Object result -eq 'Passed').Count
        if(!$matching.Count -or $passed -le 0){throw ('A declared changed-scope selector has no passing case: '+$selector)}
        $coverage.Add([pscustomobject]@{Suite=$platform;RunDirectory=$directory;Selector=$selector;Total=$matching.Count;Passed=$passed;Skipped=@($matching|Where-Object result -eq 'Skipped').Count})
    }
    foreach($test in $testCases) {
        if([string]::IsNullOrWhiteSpace([string]$test.fullname) -or [string]::IsNullOrWhiteSpace([string]$test.classname) -or [string]::IsNullOrWhiteSpace([string]$test.methodname)){throw 'Test cases need complete NUnit name metadata.'}
        $matchingSelectors=@($selectors|Where-Object{Matches-Selector $test $_})
        if(!$matchingSelectors.Count){throw ('A test case ran outside the declared changed scope: '+$test.fullname)}
        $reason=$test.SelectSingleNode('reason/message')
        $cases.Add([pscustomobject]@{Suite=$platform;RunDirectory=$directory;Name=$test.fullname;Fixture=$test.classname;Method=$test.methodname;SelectedBy=($matchingSelectors -join ';');Result=$test.result;Reason=$(if($reason){$reason.InnerText}else{''});Report=$resultsPath})
    }
    $suites.Add([pscustomobject]@{Suite=$platform;RunDirectory=$directory;TestFilter=$actualFilter;Selectors=$selectors;Total=[int]$run.total;Passed=[int]$run.passed;Failed=[int]$run.failed;Skipped=[int]$run.skipped;FullSuiteRun=$false;Report=$resultsPath;ResultsSHA256=$resultsHash;LaunchSHA256=(Get-FileHash -LiteralPath $launchPath).Hash;CompletionSHA256=(Get-FileHash -LiteralPath $completionPath).Hash;SourceManifestSHA256=$sourceHash;ProjectInputManifestSHA256=$projectHash})
}
if(!$platforms.Contains('EditMode') -or !$platforms.Contains('PlayMode')){throw 'Changed and related acceptance requires declared passing targeted evidence for both EditMode and PlayMode.'}
foreach($record in $inputFiles){if((Get-FileHash -LiteralPath $record.Path).Hash -ne $record.SHA256){throw 'Test evidence changed during verification.'}}
if((Get-FileHash -LiteralPath $scopePath).Hash -ne $scopeHash -or (Get-FileHash -LiteralPath $sourceManifest).Hash -ne $sourceHash -or (Get-FileHash -LiteralPath $projectManifest).Hash -ne $projectHash){throw 'Scope or final manifests changed during verification.'}
[IO.Directory]::CreateDirectory($evidence)|Out-Null
Copy-Item -LiteralPath $scopePath -Destination (Join-Path $evidence 'changed-scope.json')
if((Get-FileHash -LiteralPath (Join-Path $evidence 'changed-scope.json')).Hash -ne $scopeHash){throw 'Scope archival hash mismatch.'}
$cases|Export-Csv -LiteralPath (Join-Path $evidence 'verification-cases.csv') -NoTypeInformation -Encoding utf8
$coverage|Export-Csv -LiteralPath (Join-Path $evidence 'selection-coverage.csv') -NoTypeInformation -Encoding utf8
ConvertTo-Json -InputObject @($cases|Where-Object Result -eq 'Skipped') -Depth 5|Set-Content -LiteralPath (Join-Path $evidence 'skipped-tests.json') -Encoding utf8
[pscustomobject]@{
    Result='PASS';RecordedUtc=[datetime]::UtcNow.ToString('o');Scope='ChangedAndRelated';AuthorizationMode='UserRequestedTargetedOnly';UserAuthorization=[string]$scope.UserAuthorization;
    ScopeDescription='Only the declared changed and related EditMode/PlayMode selectors were verified. No full PlayMode regression is claimed. Skipped cases are listed separately and are not counted as passes. Windows player smoke and package checks follow.';
    FullEditModeRun=$false;FullPlayModeRun=$false;FullSuiteRun=$false;ScopeFile=$scopePath;ScopeFileSHA256=$scopeHash;ArchivedScope=(Join-Path $evidence 'changed-scope.json');
    Passed=($suites|Measure-Object Passed -Sum).Sum;Failed=($suites|Measure-Object Failed -Sum).Sum;Skipped=($suites|Measure-Object Skipped -Sum).Sum;Total=($suites|Measure-Object Total -Sum).Sum;
    UniquePassed=@($cases|Where-Object Result -eq 'Passed'|ForEach-Object{$_.Suite+'|'+$_.Name}|Sort-Object -Unique).Count;
    RepeatedPassedExecutions=($suites|Measure-Object Passed -Sum).Sum-@($cases|Where-Object Result -eq 'Passed'|ForEach-Object{$_.Suite+'|'+$_.Name}|Sort-Object -Unique).Count;
    Suites=@($suites);SelectionCoverage=@($coverage);SourceManifestSHA256=$sourceHash;ProjectInputManifestSHA256=$projectHash
}|ConvertTo-Json -Depth 8|Tee-Object -FilePath (Join-Path $evidence 'verification-summary.json')
