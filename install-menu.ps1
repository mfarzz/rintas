$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot 'Rintas.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Rintas.exe belum dibangun.' }
$menu = 'HKCU:\Software\Classes\*\shell\CodexFileShare'
$command = "`"$app`" --share `"%1`""
New-Item -Path $menu -Force | Out-Null
Set-Item -Path $menu -Value 'Bagikan dengan Rintas'
Set-ItemProperty -Path $menu -Name 'Icon' -Value (Join-Path $PSScriptRoot 'assets\generated\RintasLight.ico')
New-Item -Path "$menu\command" -Force | Out-Null
Set-Item -Path "$menu\command" -Value $command
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Set-ItemProperty -Path $run -Name 'Rintas' -Value "`"$app`""
Start-Process -FilePath $app -WindowStyle Hidden
Write-Host 'Menu klik kanan terpasang untuk akun Windows ini.'
Write-Host 'Di Windows 11, buka "Tampilkan opsi lainnya" jika menu belum terlihat.'
