$ErrorActionPreference='Stop'
$storyWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$storyOutput=Join-Path $storyWorkspace 'outputs/story-text-fix-20261103'
$storyEdits=Get-Content -LiteralPath (Join-Path $storyOutput 'card-flavor-edits.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$storyHashes=@()
foreach($storyGroup in @($storyEdits | Group-Object file)) {
    $storySource=Join-Path $storyWorkspace $storyGroup.Name
    $storyBackup=Join-Path $storyOutput ('input-backup/'+$storyGroup.Name)
    if(Test-Path -LiteralPath $storyBackup){throw ('Backup exists; do not replace: '+$storyBackup)}
    $storyBefore=[IO.File]::ReadAllText($storySource)
    $storyAfter=$storyBefore
    foreach($storyEdit in $storyGroup.Group) {
        if([regex]::Matches($storyAfter,[regex]::Escape($storyEdit.oldText)).Count -ne 1){throw ('Expected one exact input for '+$storyGroup.Name+': '+$storyEdit.oldText)}
        $storyAfter=$storyAfter.Replace($storyEdit.oldText,$storyEdit.newText)
    }
    if($storyGroup.Name.EndsWith('.csv')) {
        $storyOriginalRows=@($storyBefore | ConvertFrom-Csv)
        $storyNewRows=@($storyAfter | ConvertFrom-Csv)
        if($storyOriginalRows.Count -ne $storyNewRows.Count){throw 'CSV row count changed.'}
        for($storyIndex=0;$storyIndex -lt $storyOriginalRows.Count;$storyIndex++) {
            foreach($storyProperty in $storyOriginalRows[$storyIndex].PSObject.Properties) {
                if($storyProperty.Name -ne 'description' -and $storyProperty.Value -cne $storyNewRows[$storyIndex].($storyProperty.Name)){throw ('Non-description CSV field changed: '+$storyProperty.Name)}
            }
        }
    } else {
        $storyLiteral='"(?:\\.|[^"\\])*"'
        if([regex]::Replace($storyBefore,$storyLiteral,'""') -cne [regex]::Replace($storyAfter,$storyLiteral,'""')){throw 'C# code outside strings changed.'}
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $storyBackup) | Out-Null
    Copy-Item -LiteralPath $storySource -Destination $storyBackup
    $storyBeforeHash=(Get-FileHash -LiteralPath $storySource -Algorithm SHA256).Hash
    if((Get-FileHash -LiteralPath $storyBackup -Algorithm SHA256).Hash -ne $storyBeforeHash){throw 'Backup differs.'}
    $storyBytes=[IO.File]::ReadAllBytes($storySource)
    $storyHasBom=$storyBytes.Length -ge 3 -and $storyBytes[0] -eq 239 -and $storyBytes[1] -eq 187 -and $storyBytes[2] -eq 191
    [IO.File]::WriteAllText($storySource,$storyAfter,[Text.UTF8Encoding]::new($storyHasBom))
    $storyHashes+=[pscustomobject]@{Path=$storySource;BeforeSHA256=$storyBeforeHash;AfterSHA256=(Get-FileHash -LiteralPath $storySource -Algorithm SHA256).Hash;TextChanges=$storyGroup.Count}
}
$storyHashes | Export-Csv -LiteralPath (Join-Path $storyOutput 'input-backup/card-flavor-hashes.csv') -NoTypeInformation -Encoding UTF8
Write-Output ('PASS: '+$storyEdits.Count+' flavor edits; original bytes backed up; card code and all non-description CSV fields unchanged.')
