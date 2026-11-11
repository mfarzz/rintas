param(
    [Parameter(Mandatory = $true)][string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$thumbprintClean = $Thumbprint.Replace(' ', '').ToUpperInvariant()
$certificate = Get-ChildItem "Cert:\CurrentUser\My\$thumbprintClean" -ErrorAction SilentlyContinue
$machineStore = $false
if (-not $certificate) {
    $certificate = Get-ChildItem "Cert:\LocalMachine\My\$thumbprintClean" -ErrorAction SilentlyContinue
    $machineStore = $true
}
if (-not $certificate) { throw 'Sertifikat tidak ditemukan di penyimpanan Personal Windows.' }
if (-not $certificate.HasPrivateKey) { throw 'Sertifikat tidak memiliki private key yang dapat dipakai.' }
if ($certificate.NotAfter -le (Get-Date)) { throw 'Sertifikat sudah kedaluwarsa.' }
if (-not ($certificate.EnhancedKeyUsageList.FriendlyName -contains 'Code Signing')) { throw 'Sertifikat bukan untuk Code Signing.' }

$signTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe'
$installer = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'FileShareSetup.exe'
if (-not (Test-Path -LiteralPath $signTool)) { throw 'SignTool tidak ditemukan.' }
if (-not (Test-Path -LiteralPath $installer)) { throw 'FileShareSetup.exe belum dibangun.' }

$arguments = @('sign', '/fd', 'SHA256', '/sha1', $thumbprintClean, '/s', 'My', '/tr', $TimestampUrl, '/td', 'SHA256')
if ($machineStore) { $arguments += '/sm' }
$arguments += $installer
& $signTool @arguments
if ($LASTEXITCODE -ne 0) { throw 'Gagal menandatangani installer.' }
& $signTool verify /pa $installer
if ($LASTEXITCODE -ne 0) { throw 'Verifikasi tanda tangan gagal.' }
Write-Host 'Installer berhasil ditandatangani dan diverifikasi.'
