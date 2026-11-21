param([switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
function Invoke-DotNet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet command failed with exit code $LASTEXITCODE" } }
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & (Join-Path $PSScriptRoot 'Prepare-Speech.ps1')
    Invoke-DotNet restore HekaSomi.Lab.sln --locked-mode --disable-parallel -p:BuildInParallel=false
    Invoke-DotNet build HekaSomi.Lab.sln -c Release --no-restore -m:1
    Invoke-DotNet test tests/HekaSomi.Lab.Tests -c Release --no-build --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/test-results
    if (-not $SkipPublish) {
        Invoke-DotNet publish src/HekaSomi.Lab.Desktop/HekaSomi.Lab.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -m:1 -o artifacts/windows-x64
        Copy-Item README.md artifacts/windows-x64/README.md -Force
        Copy-Item START-HERE.txt artifacts/windows-x64/START-HERE.txt -Force
        New-Item -ItemType Directory -Force -Path artifacts/windows-x64/docs | Out-Null
        Copy-Item docs/* artifacts/windows-x64/docs -Recurse -Force
        Write-Host 'Open artifacts/windows-x64/HekaSomi.Lab.exe on Windows.'
    }
} finally { Pop-Location }
