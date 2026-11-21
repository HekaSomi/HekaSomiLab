$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path $PSScriptRoot -Parent) 'assets/whisper'
$manifestPath = Join-Path $root 'bundle.json'
if (-not (Test-Path $manifestPath)) { throw 'Included speech files are missing. Extract the complete source ZIP again.' }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
foreach ($name in @($manifest.executable, $manifest.model)) {
    $entry = $manifest.sha256.PSObject.Properties[$name]
    if (-not $entry) { throw "Missing checksum for $name" }
    $path = Join-Path $root $name
    if (-not (Test-Path $path)) { throw "Missing included speech file: $name" }
    if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $entry.Value) { throw "Damaged included speech file: $name" }
}
Write-Host 'Included speech engine and multilingual model verified. No download is needed.'
