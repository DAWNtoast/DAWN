#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskArtRoot='D:/Game/Artifacts/DepartureDockLayers4K20261013'
$taskNames=@('dock-base','chart-shattered-reef','chart-golden-trade','chart-moon-ruins')
$taskLabels=@('断锚港码头无字底图','碎帆礁海','黄金潮汐商路','月潮遗迹海')
$taskAssets=@()
Add-Type -AssemblyName System.Drawing
for($taskIndex=0; $taskIndex -lt $taskNames.Count; $taskIndex++) {
    $taskName=$taskNames[$taskIndex]
    $taskApiMetadataPath=Join-Path $taskArtRoot ('google-originals/'+$taskName+'.png.metadata.json')
    $taskApi=Get-Content -LiteralPath $taskApiMetadataPath -Raw | ConvertFrom-Json
    if(-not $taskApi.success -or $taskApi.httpStatus -ne 200) {throw ('Generation did not succeed: '+$taskName)}
    $taskExport=Get-Content -LiteralPath (Join-Path $taskArtRoot ('final-4k/'+$taskName+'.export.json')) -Raw | ConvertFrom-Json
    if($taskExport.upscaled) {throw ('Unexpected upscaling: '+$taskName)}
    $taskImage=[Drawing.Image]::FromFile($taskExport.output)
    try { if($taskImage.Width -ne 3840 -or $taskImage.Height -ne 2160 -or $taskImage.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid) {throw ('Invalid final format: '+$taskName)} } finally {$taskImage.Dispose()}
    if((Get-FileHash -LiteralPath $taskExport.output -Algorithm SHA256).Hash -ne $taskExport.sha256) {throw ('Final hash mismatch: '+$taskName)}
    if((Get-FileHash -LiteralPath $taskApi.output.path -Algorithm SHA256).Hash -ne $taskApi.output.sha256) {throw ('Source hash mismatch: '+$taskName)}
    $taskAssets += [ordered]@{
        id=$taskName; label=$taskLabels[$taskIndex]; image=('final-4k/'+$taskName+'.png')
        width=3840; height=2160; format='PNG'; sha256=$taskExport.sha256
        original=$taskApi.output; normalizedExport=$taskExport
        prompt=('prompts/google-'+$taskName+'.txt'); metadata=('google-originals/'+$taskName+'.png.metadata.json')
        model=$taskApi.model; requestedSizeTier=$taskApi.requestedResolution; apiAttempts=$taskApi.attempts
        usage=$taskApi.usageMetadata
        review=@{ visual=$true; noBakedText=$true; consistentStyle=$true }
    }
}
$taskManifest=[ordered]@{
    version=1; date='2026-10-13'; timezone='Asia/Shanghai'; completed=$true
    generator='Google official Gemini API'; model='gemini-3-pro-image'
    mode='Explicitly user-selected official API; not the built-in imagegen tool and not a third-party gateway.'
    endpoint='https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image:generateContent'
    credentials='Read from the local Windows user environment; no key is stored in these files.'
    scope='Four independent textless art assets. No Unity runtime files were changed in this generation phase.'
    header='Use the existing common header prefab and theme; no header artwork is baked into the new base.'
    text='All labels, numbers, route names, titles and descriptions must be rendered as separate UI text.'
    ship='Keep the existing independently rendered ship layer; the central berth is empty in the base.'
    map='The large selected-map window is blank in the base; three standalone map assets share a blank right-hand text area.'
    resolution='Original API files are preserved at 5504x3072. Final PNGs are 3840x2160, produced by a small centered aspect-ratio crop and downsampling; no upscaling.'
    sourceDrafts='The source/ files are earlier 1672x941 reference drafts, not final 4K deliverables.'
    geometry='New generated art is reference-aligned, not pixel-identical. text-overlay-spec.json contains prior-layout coordinates that must be verified before runtime integration.'
    assets=$taskAssets
}
[IO.File]::WriteAllText((Join-Path $taskArtRoot 'manifest.json'), ($taskManifest | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
Write-Output ('Verified '+$taskAssets.Count+' final 3840x2160 PNG assets and all original hashes.')
