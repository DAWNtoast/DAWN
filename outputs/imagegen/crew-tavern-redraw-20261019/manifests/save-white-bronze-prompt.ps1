param([string]$TaskRowJson)
$taskBase = 'D:/Game/outputs/imagegen/crew-tavern-redraw-20261019'
$taskRow = $TaskRowJson | ConvertFrom-Json
$taskManifest = Get-Content -Raw -LiteralPath (Join-Path $taskBase 'manifest.json') | ConvertFrom-Json
$taskLorePath = 'D:/Game/UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs'
$taskLoreLines = Get-Content -LiteralPath $taskLorePath
New-Item -ItemType Directory -Force -Path (Join-Path $taskBase 'prompts'), (Join-Path $taskBase 'manifests') | Out-Null
$taskEntry = $taskManifest | Where-Object { $_.id -eq $taskRow.id }
$taskPromptPath = Join-Path $taskBase ('prompts/' + $taskRow.id + '.txt')
[System.IO.File]::WriteAllText($taskPromptPath, $taskRow.prompt_text, [System.Text.UTF8Encoding]::new($false))
$taskLine = $taskLoreLines | Where-Object { $_ -like ('*Crew("' + $taskRow.id + '"*') } | Select-Object -First 1
$taskStrings = [regex]::Matches($taskLine, '"([^"]*)"')
$taskStoryBasis = $taskStrings[6].Groups[1].Value
$taskMetaHash = (Get-FileHash -LiteralPath ($taskEntry.target + '.meta') -Algorithm SHA256).Hash.ToLowerInvariant()
$taskTargetHash = (Get-FileHash -LiteralPath $taskEntry.target -Algorithm SHA256).Hash.ToLowerInvariant()
$taskAttempts = 0
if ($taskRow.id -eq 'crew_white_shell_navigator_saina') { $taskAttempts = 2 }
$taskRecord = [pscustomobject]@{ id=$taskRow.id; name=$taskEntry.name; port=$taskEntry.port; source=$taskEntry.reference; reference=$taskEntry.reference; target=$taskEntry.target; final=$null; prompt=$taskPromptPath; prompt_text=$taskRow.prompt_text; story_source=$taskLorePath; story_basis=$taskStoryBasis; scene=$taskRow.scene; scene_reasoning=$taskRow.reason; identity_basis=$taskRow.identity; status='pending_tool_blocked'; tool='builtin image_gen'; tool_error='network error: error sending request'; attempts=$taskAttempts; failed_attempt_prompt= $(if($taskAttempts -gt 0){'prompts/crew_white_shell_navigator_saina.attempt-20261019.txt'}else{$null}); generated_count=0; sha256=$null; size=$null; source_sha256=(Get-FileHash -LiteralPath $taskEntry.reference -Algorithm SHA256).Hash.ToLowerInvariant(); target_sha256=$taskTargetHash; target_unchanged=($taskTargetHash -eq $taskEntry.before_sha256); meta_sha256=$taskMetaHash; meta_unchanged=($taskMetaHash -eq $taskEntry.meta_sha256) }
$taskOutputPath = Join-Path $taskBase 'manifests/white-bronze.json'
$taskRecords = @()
if(Test-Path -LiteralPath $taskOutputPath) { $taskRecords = @((Get-Content -Raw -LiteralPath $taskOutputPath | ConvertFrom-Json) | Where-Object {$_.id -ne $taskRow.id}) }
$taskRecords += $taskRecord
[System.IO.File]::WriteAllText($taskOutputPath, (ConvertTo-Json -InputObject @($taskRecords) -Depth 8), [System.Text.UTF8Encoding]::new($false))
$taskRecord | Select-Object id,status,attempts,target_unchanged,meta_unchanged | ConvertTo-Json
