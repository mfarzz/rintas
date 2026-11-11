$menu = 'HKCU:\Software\Classes\*\shell\CodexFileShare'
if (Test-Path $menu) { Remove-Item -LiteralPath $menu -Recurse -Force }
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty -Path $run -Name 'Rintas' -ErrorAction SilentlyContinue) { Remove-ItemProperty -Path $run -Name 'Rintas' }
Write-Host 'Menu klik kanan dilepas.'
