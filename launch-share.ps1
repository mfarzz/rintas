param([Parameter(Mandatory = $true)][string]$FilePath)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
try {
    $node = Join-Path $PSScriptRoot 'node.exe'
    if (-not (Test-Path -LiteralPath $node)) {
        $node = (Get-Command node -ErrorAction SilentlyContinue).Source
    }
    if (-not $node) { throw 'Node.js tidak ditemukan.' }
    & $node '.\share.js' $FilePath
    if ($LASTEXITCODE -ne 0) { Read-Host 'Tekan Enter untuk menutup jendela' }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Read-Host 'Tekan Enter untuk menutup jendela'
}
