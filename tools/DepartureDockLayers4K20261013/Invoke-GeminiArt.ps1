#Requires -Version 5.1
<#
.SYNOPSIS
One explicitly requested Gemini image-generation call; no automatic retries.
.EXAMPLE
powershell -NoProfile -File .\Invoke-GeminiArt.ps1 -PromptFile .\dock.txt -OutputPath .\dock-4k.png -ReferenceImages .\reference.png
.NOTES
Reads GEMINI_API_KEY from Windows user variables, then the current process.
Sends the prompt and reference images only to Google's fixed HTTPS endpoint.
Does not upscale, resize, edit, or rewrite returned image bytes. The API's 4K
tier does not promise exactly 3840x2160; metadata records actual dimensions.
Official schema: https://ai.google.dev/api/generate-content
Run as a separate PowerShell process because failures return a nonzero exit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PromptFile,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string[]]$ReferenceImages = @(),
    [ValidateSet('1K', '2K', '4K')][string]$Resolution = '4K',
    [ValidateSet('1:1', '2:3', '3:2', '3:4', '4:3', '4:5', '5:4', '9:16', '16:9', '21:9')]
    [string]$AspectRatio = '16:9',
    [ValidateRange(30, 1800)][int]$TimeoutSeconds = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$model = 'gemini-3-pro-image'
$endpoint = 'https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image:generateContent'
$apiKey = $null
$client = $null
$handler = $null
$request = $null
$response = $null
$lockStream = $null
$metadataPath = $null
$lockPath = $null
$metadata = $null
$exitCode = 1
$category = 'local_validation'

function Get-PropertyValue($Object, [string]$Name) {
    if ($null -ne $Object) {
        $property = $Object.PSObject.Properties[$Name]
        if ($null -ne $property) { return $property.Value }
    }
    return $null
}

function Protect-Text([string]$Value) {
    if ([string]::IsNullOrEmpty($Value)) { return $Value }
    if (-not [string]::IsNullOrEmpty($script:apiKey)) {
        $Value = $Value.Replace($script:apiKey, '[REDACTED]')
    }
    $Value = [regex]::Replace($Value, 'AIza[A-Za-z0-9_-]{20,}', '[REDACTED]')
    return [regex]::Replace($Value, '[A-Za-z0-9+/]{160,}={0,2}', '[BINARY OMITTED]')
}

function Write-NewBytes([string]$Path, [byte[]]$Bytes) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($Bytes, 0, $Bytes.Length) }
    finally { $stream.Dispose() }
}

function Get-ImageDimensions([byte[]]$Bytes) {
    # GDI+ reads the actual encoded image, not requested API dimensions.
    Add-Type -AssemblyName System.Drawing
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    $decoded = $null
    try {
        $decoded = [Drawing.Image]::FromStream($stream, $false, $true)
        return @{ width = $decoded.Width; height = $decoded.Height }
    }
    finally {
        if ($null -ne $decoded) { $decoded.Dispose() }
        $stream.Dispose()
    }
}

try {
    $outputFullPath = [IO.Path]::GetFullPath($OutputPath)
    $metadataPath = $outputFullPath + '.metadata.json'
    $outputStem = [IO.Path]::Combine([IO.Path]::GetDirectoryName($outputFullPath), [IO.Path]::GetFileNameWithoutExtension($outputFullPath))
    $lockPath = $outputStem + '.request.lock'
    $protectedPaths = @($outputFullPath, $metadataPath)
    foreach ($imageExtension in @('.png', '.jpg', '.jpeg', '.webp')) {
        $protectedPaths += ($outputStem + $imageExtension)
        $protectedPaths += ($outputStem + $imageExtension + '.metadata.json')
    }
    foreach ($protectedPath in $protectedPaths) {
        if ([IO.File]::Exists($protectedPath)) {
            throw 'Output or metadata already exists; choose a new versioned filename.'
        }
    }
    $promptFullPath = (Resolve-Path -LiteralPath $PromptFile).Path
    $prompt = [IO.File]::ReadAllText($promptFullPath, [Text.Encoding]::UTF8)
    if ([string]::IsNullOrWhiteSpace($prompt)) { throw 'The prompt is empty.' }
    $apiKey = [Environment]::GetEnvironmentVariable('GEMINI_API_KEY', 'User')
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $apiKey = [Environment]::GetEnvironmentVariable('GEMINI_API_KEY', 'Process')
    }
    if ([string]::IsNullOrWhiteSpace($apiKey)) { throw 'GEMINI_API_KEY is not configured.' }
    $apiKey = $apiKey.Trim()
    if ($prompt.Contains($apiKey)) { throw 'The prompt unexpectedly contains the API key.' }

    $parts = [Collections.Generic.List[object]]::new()
    $parts.Add(@{ text = $prompt })
    $references = [Collections.Generic.List[object]]::new()
    $inputBytes = 0L
    foreach ($reference in $ReferenceImages) {
        $referenceFullPath = (Resolve-Path -LiteralPath $reference).Path
        $extension = [IO.Path]::GetExtension($referenceFullPath).ToLowerInvariant()
        $mime = switch ($extension) {
            '.png' { 'image/png' }
            '.jpg' { 'image/jpeg' }
            '.jpeg' { 'image/jpeg' }
            '.webp' { 'image/webp' }
            default { throw 'Reference images must be PNG, JPEG, or WebP.' }
        }
        $bytes = [IO.File]::ReadAllBytes($referenceFullPath)
        $inputBytes += $bytes.LongLength
        $parts.Add(@{ inlineData = @{ mimeType = $mime; data = [Convert]::ToBase64String($bytes) } })
        $references.Add(@{ path = $referenceFullPath; bytes = $bytes.LongLength; sha256 = (Get-FileHash -LiteralPath $referenceFullPath -Algorithm SHA256).Hash })
    }
    # Inline request limit includes JSON/base64 expansion. Reject locally before charging.
    $body = @{
        contents = @(@{ role = 'user'; parts = $parts.ToArray() })
        generationConfig = @{
            responseModalities = @('TEXT', 'IMAGE')
            imageConfig = @{ aspectRatio = $AspectRatio; imageSize = $Resolution }
        }
    } | ConvertTo-Json -Depth 20 -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($body) -gt 19000000) {
        throw 'Inline JSON exceeds the conservative 19 MB request limit; use smaller references.'
    }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputFullPath))
    $lockStream = [IO.File]::Open($lockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    # Recheck after locking to protect against another runner using the same path.
    foreach ($protectedPath in $protectedPaths) {
        if ([IO.File]::Exists($protectedPath)) { throw 'Output is already reserved.' }
    }
    $metadata = [ordered]@{
        model = $model; requestedResolution = $Resolution; requestedAspectRatio = $AspectRatio
        endpoint = $endpoint; startedUtc = [DateTime]::UtcNow.ToString('o')
        promptFile = $promptFullPath; promptSha256 = (Get-FileHash -LiteralPath $promptFullPath -Algorithm SHA256).Hash
        references = $references.ToArray(); attempts = 1; automaticRetries = $false
        success = $false; httpStatus = $null; errorClassification = $null
        usageMetadata = $null; finishReasons = @(); text = @(); output = $null
    }
    Add-Type -AssemblyName System.Net.Http
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, $endpoint)
    [void]$request.Headers.TryAddWithoutValidation('x-goog-api-key', $apiKey)
    $request.Content = [Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/json')
    $category = 'network_or_timeout'
    # Exactly one paid request. A timeout has unknown server-side outcome; do not retry automatically.
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $metadata.httpStatus = [int]$response.StatusCode
    $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        $category = switch ([int]$response.StatusCode) {
            400 { 'invalid_request' }; 401 { 'authentication' }; 403 { 'permission_or_billing' }
            404 { 'model_not_found' }; 429 { 'quota_or_rate_limit' }
            { $_ -ge 500 } { 'service_unavailable' }; default { 'http_error' }
        }
        # Keep only the API's concise error message, never headers or complete raw response.
        try {
            $errorBody = $responseBody | ConvertFrom-Json
            $apiError = Get-PropertyValue $errorBody 'error'
            $message = Protect-Text ([string](Get-PropertyValue $apiError 'message'))
            if ($message.Length -gt 2000) { $message = $message.Substring(0, 2000) }
            $metadata.apiErrorMessage = $message
            $metadata.apiErrorStatus = Protect-Text ([string](Get-PropertyValue $apiError 'status'))
        } catch { }
        throw 'Google rejected the request. See the sanitized metadata.'
    }
    $category = 'invalid_response'
    $result = $responseBody | ConvertFrom-Json
    $metadata.usageMetadata = Get-PropertyValue $result 'usageMetadata'
    $metadata.modelVersion = Protect-Text ([string](Get-PropertyValue $result 'modelVersion'))
    $imageParts = [Collections.Generic.List[object]]::new()
    $texts = [Collections.Generic.List[string]]::new()
    $finishReasons = [Collections.Generic.List[string]]::new()
    foreach ($candidate in @(Get-PropertyValue $result 'candidates')) {
        if ($null -eq $candidate) { continue }
        $finishReasons.Add([string](Get-PropertyValue $candidate 'finishReason'))
        $content = Get-PropertyValue $candidate 'content'
        foreach ($part in @(Get-PropertyValue $content 'parts')) {
            if ($null -eq $part -or (Get-PropertyValue $part 'thought') -eq $true) { continue }
            $partText = Get-PropertyValue $part 'text'
            if ($null -ne $partText) { $texts.Add((Protect-Text ([string]$partText))) }
            $inlineData = Get-PropertyValue $part 'inlineData'
            if ($null -ne $inlineData -and ([string](Get-PropertyValue $inlineData 'mimeType')).StartsWith('image/')) {
                $imageParts.Add($inlineData)
            }
        }
    }
    $metadata.text = $texts.ToArray()
    $metadata.finishReasons = $finishReasons.ToArray()
    $metadata.imagePartCount = $imageParts.Count
    if ($imageParts.Count -eq 0) {
        $category = 'no_image_returned'
        $feedback = Get-PropertyValue $result 'promptFeedback'
        $metadata.blockReason = Protect-Text ([string](Get-PropertyValue $feedback 'blockReason'))
        throw 'The response contained no final image.'
    }
    # Persist the first final image verbatim. Thought images are never exported.
    $imageMime = ([string](Get-PropertyValue $imageParts[0] 'mimeType')).ToLowerInvariant()
    $imageExtension = switch ($imageMime) {
        'image/png' { '.png' }
        'image/jpeg' { '.jpg' }
        'image/webp' { '.webp' }
        default { throw 'The returned image MIME type is unsupported.' }
    }
    # The requested filename chooses the stem; the API chooses the actual format.
    # Metadata keeps its requested-name location for discoverability.
    $outputFullPath = $outputStem + $imageExtension
    $imageBytes = [Convert]::FromBase64String([string](Get-PropertyValue $imageParts[0] 'data'))
    $category = 'output_write'
    Write-NewBytes $outputFullPath $imageBytes
    $metadata.output = [ordered]@{
        path = $outputFullPath; mimeType = $imageMime
        bytes = $imageBytes.LongLength; sha256 = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash
        width = $null; height = $null; resized = $false
    }
    $category = 'image_decode'
    $dimensions = Get-ImageDimensions $imageBytes
    $metadata.output.width = $dimensions.width
    $metadata.output.height = $dimensions.height
    $metadata.success = $true
    $category = $null
    $exitCode = 0
}
catch {
    # Do not format $_, exceptions, request objects, or headers: they may contain secrets.
    if ($null -ne $metadata) { $metadata.errorClassification = $category }
    [Console]::Error.WriteLine('Gemini image request failed: ' + $category + '. No automatic retry was made.')
}
finally {
    if ($null -ne $metadata) {
        $metadata.completedUtc = [DateTime]::UtcNow.ToString('o')
        try {
            $metadataJson = Protect-Text ($metadata | ConvertTo-Json -Depth 20)
            Write-NewBytes $metadataPath ([Text.UTF8Encoding]::new($false).GetBytes($metadataJson))
        } catch {
            $exitCode = 1
            [Console]::Error.WriteLine('Could not save metadata. Any returned image already saved has been preserved.')
        }
    }
    if ($null -ne $response) { $response.Dispose() }
    if ($null -ne $request) { $request.Dispose() }
    if ($null -ne $client) { $client.Dispose() }
    elseif ($null -ne $handler) { $handler.Dispose() }
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
        [IO.File]::Delete($lockPath)
    }
    $apiKey = $null
}

if ($exitCode -eq 0) {
    Write-Output ('Saved image: ' + $outputFullPath)
    Write-Output ('Actual pixels: ' + $metadata.output.width + 'x' + $metadata.output.height)
    Write-Output ('Metadata: ' + $metadataPath)
}
exit $exitCode
