$ErrorActionPreference = 'Stop'
$target = $PSScriptRoot
$expected = Join-Path $env:LOCALAPPDATA 'FileShareTunnel'
if (-not [string]::Equals([System.IO.Path]::GetFullPath($target).TrimEnd('\'), [System.IO.Path]::GetFullPath($expected).TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Lokasi pemasangan tidak sesuai; pembatalan dihentikan.'
}
$menu = 'HKCU:\Software\Classes\*\shell\FileShareTunnel'
if (Test-Path $menu) { Remove-Item -LiteralPath $menu -Recurse -Force }
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty -Path $run -Name 'Rintas' -ErrorAction SilentlyContinue) { Remove-ItemProperty -Path $run -Name 'Rintas' }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FileShareTunnel'
if (Test-Path $uninstallKey) { Remove-Item -LiteralPath $uninstallKey -Recurse -Force }
Remove-Item -LiteralPath $target -Recurse -Force
Write-Host 'Aplikasi telah dihapus.'
