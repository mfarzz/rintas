$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stage = Join-Path $PSScriptRoot 'stage'
$payloadZip = Join-Path $PSScriptRoot 'payload.zip'
$output = Join-Path $root 'FileShareSetup.exe'
$node = 'C:\Program Files\nodejs\node.exe'
$cloudflared = Join-Path $root 'cloudflared.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$icon = Join-Path $root 'assets\generated\Rintas.ico'
$brandMarkDark = Join-Path $root 'assets\rintas-mark.png'
$brandMarkLight = Join-Path $root 'assets\rintas-mark-1.png'

if (-not (Test-Path -LiteralPath $node)) { throw 'Node.js tidak ditemukan di C:\Program Files\nodejs\node.exe.' }
if (-not (Test-Path -LiteralPath $cloudflared)) { throw 'cloudflared.exe tidak ditemukan di folder file-share.' }
if (-not (Test-Path -LiteralPath $csc)) { throw 'Compiler .NET Framework tidak ditemukan.' }
if (-not (Test-Path -LiteralPath $icon)) { throw 'Ikon Rintas belum dibangun. Jalankan python .\assets\build-icons.py.' }
if (-not (Test-Path -LiteralPath $brandMarkDark) -or -not (Test-Path -LiteralPath $brandMarkLight)) { throw 'Logo Rintas tidak ditemukan.' }
if ((Get-AuthenticodeSignature -LiteralPath $cloudflared).Status -ne 'Valid') { throw 'Tanda tangan digital cloudflared.exe tidak valid.' }

New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    $app = Join-Path $root 'Rintas.exe'
    $appArguments = @('/nologo', '/target:winexe', '/platform:x64', "/out:$app", "/win32icon:$icon", '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll', (Join-Path $root 'RintasApp.cs'))
    & $csc @appArguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $app)) { throw 'Gagal membangun Rintas.exe.' }
    Copy-Item -LiteralPath $node -Destination (Join-Path $stage 'node.exe') -Force
    Copy-Item -LiteralPath $cloudflared -Destination (Join-Path $stage 'cloudflared.exe') -Force
    foreach ($name in @('Rintas.exe', 'share.js', 'config.json', 'README.md')) {
        Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $stage $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $root 'docs\PANDUAN-DOMAIN-RINTAS.md') -Destination (Join-Path $stage 'PANDUAN-DOMAIN-RINTAS.md') -Force
    $assets = Join-Path $stage 'Assets'
    New-Item -ItemType Directory -Path $assets -Force | Out-Null
    foreach ($name in @('RintasDark.ico', 'RintasLight.ico')) {
        Copy-Item -LiteralPath (Join-Path $root "assets\generated\$name") -Destination (Join-Path $assets $name) -Force
    }
    Copy-Item -LiteralPath $brandMarkDark -Destination (Join-Path $assets 'BrandMarkDark.png') -Force
    Copy-Item -LiteralPath $brandMarkLight -Destination (Join-Path $assets 'BrandMarkLight.png') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall-app.ps1') -Destination (Join-Path $stage 'uninstall-app.ps1') -Force
    if (Test-Path -LiteralPath $payloadZip) { Remove-Item -LiteralPath $payloadZip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $payloadZip -CompressionLevel Optimal
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
    $arguments = @('/nologo', '/target:winexe', '/platform:x64', "/out:$output", "/win32icon:$icon", "/resource:$payloadZip,Payload", '/r:System.Drawing.dll', '/r:System.IO.Compression.dll', '/r:System.Windows.Forms.dll', (Join-Path $PSScriptRoot 'Installer.cs'))
    & $csc @arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw 'Compiler gagal membuat installer.' }
    $item = Get-Item -LiteralPath $output
    Write-Host "Installer siap: $($item.FullName) ($([math]::Round($item.Length / 1MB, 1)) MB)"
} finally {
    $resolvedStage = [System.IO.Path]::GetFullPath($stage)
    $resolvedInstaller = [System.IO.Path]::GetFullPath($PSScriptRoot)
    if ($resolvedStage.StartsWith($resolvedInstaller + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $stage)) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    if (Test-Path -LiteralPath $payloadZip) { Remove-Item -LiteralPath $payloadZip -Force }
}
