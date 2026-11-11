$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stage = Join-Path $PSScriptRoot 'stage'
$output = Join-Path $root 'Rintas_1.0.9.0_x64.msix'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$makeAppx = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\makeappx.exe'
$node = 'C:\Program Files\nodejs\node.exe'
$msvc = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231'
$kit = 'C:\Program Files (x86)\Windows Kits\10'
$cl = Join-Path $msvc 'bin\Hostx64\x64\cl.exe'
$icon = Join-Path $root 'assets\generated\Rintas.ico'
$brandMarkDark = Join-Path $root 'assets\rintas-mark.png'
$brandMarkLight = Join-Path $root 'assets\rintas-mark-1.png'
foreach ($required in @($csc, $makeAppx, $node, $cl, $icon, $brandMarkDark, $brandMarkLight, (Join-Path $root 'cloudflared.exe'))) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Berkas wajib tidak ditemukan: $required" }
}

New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    $app = Join-Path $root 'Rintas.exe'
    $args = @('/nologo', '/target:winexe', '/platform:x64', "/out:$app", "/win32icon:$icon", '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll', (Join-Path $root 'RintasApp.cs'))
    & $csc @args
    if ($LASTEXITCODE -ne 0) { throw 'Gagal membangun Rintas.exe.' }
    $env:INCLUDE = "$msvc\include;$kit\Include\10.0.28000.0\um;$kit\Include\10.0.28000.0\shared;$kit\Include\10.0.28000.0\ucrt;$kit\Include\10.0.28000.0\winrt"
    $env:LIB = "$msvc\lib\x64;$kit\Lib\10.0.28000.0\um\x64;$kit\Lib\10.0.28000.0\ucrt\x64"
    $env:PATH = "$msvc\bin\Hostx64\x64;$env:PATH"
    $dll = Join-Path $PSScriptRoot 'RintasExplorer.dll'
    $obj = Join-Path $PSScriptRoot 'RintasExplorer.obj'
    $lib = Join-Path $PSScriptRoot 'RintasExplorer.lib'
    & $cl /nologo /TC /LD /O2 /DUNICODE /D_UNICODE "/Fo:$obj" "/Fe:$dll" (Join-Path $PSScriptRoot 'RintasExplorer.c') /link "/DEF:$(Join-Path $PSScriptRoot 'RintasExplorer.def')" "/IMPLIB:$lib" ole32.lib shell32.lib uuid.lib advapi32.lib
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dll)) { throw 'Gagal membangun ekstensi menu Explorer.' }
    Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'RintasExplorer.dll') -Force
    foreach ($name in @('Rintas.exe', 'share.js', 'config.json', 'cloudflared.exe', 'README.md')) {
        Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $stage $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $root 'docs\PANDUAN-DOMAIN-RINTAS.md') -Destination (Join-Path $stage 'PANDUAN-DOMAIN-RINTAS.md') -Force
    Copy-Item -LiteralPath $node -Destination (Join-Path $stage 'node.exe') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Destination (Join-Path $stage 'AppxManifest.xml') -Force

    $assets = Join-Path $stage 'Assets'
    New-Item -ItemType Directory -Path $assets -Force | Out-Null
    foreach ($name in @('Square44x44Logo.png', 'Square150x150Logo.png', 'StoreLogo.png', 'RintasDark.ico', 'RintasLight.ico')) {
        Copy-Item -LiteralPath (Join-Path $root "assets\generated\$name") -Destination (Join-Path $assets $name) -Force
    }
    Copy-Item -LiteralPath $brandMarkDark -Destination (Join-Path $assets 'BrandMarkDark.png') -Force
    Copy-Item -LiteralPath $brandMarkLight -Destination (Join-Path $assets 'BrandMarkLight.png') -Force

    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
    & $makeAppx pack /d $stage /p $output /o
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw 'MakeAppx gagal membuat paket MSIX.' }
    $item = Get-Item -LiteralPath $output
    Write-Host "MSIX siap: $($item.FullName) ($([math]::Round($item.Length / 1MB, 1)) MB)"
} finally {
    $resolvedStage = [System.IO.Path]::GetFullPath($stage)
    $resolvedMsix = [System.IO.Path]::GetFullPath($PSScriptRoot)
    if ($resolvedStage.StartsWith($resolvedMsix + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $stage)) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
