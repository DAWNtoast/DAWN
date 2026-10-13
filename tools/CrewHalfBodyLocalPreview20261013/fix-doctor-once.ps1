$ErrorActionPreference = 'Stop'
$cardSource = 'D:\Game\UnityClient\Assets\_Project\Resources\CardArt\Western\card_doctor_sutures.png'
$cardOutput = 'D:\Game\Artifacts\CardDoctorAnatomy20261013'
$cardBackup = Join-Path $cardOutput 'original-card_doctor_sutures.png'
if (-not (Test-Path -LiteralPath $cardBackup)) {
    Copy-Item -LiteralPath $cardSource -Destination $cardBackup
    Copy-Item -LiteralPath ($cardSource + '.meta') -Destination (Join-Path $cardOutput 'original-card_doctor_sutures.png.meta')
}
& 'D:\Game\tools\BackgroundClarity4K20261013\Invoke-GeminiEnhancement.ps1' -PromptFile (Join-Path $cardOutput 'prompt.txt') -OutputPath (Join-Path $cardOutput 'doctor-sutures-anatomy-v2.png') -ReferenceImages @($cardBackup) -Resolution '2K' -AspectRatio '1:1' -TimeoutSeconds 300
