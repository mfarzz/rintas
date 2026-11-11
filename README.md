# Rintas

Rintas membagikan file langsung dari laptop melalui Cloudflare Tunnel. File tidak disalin ke penyimpanan cloud. Aplikasi berjalan di system tray tanpa terminal. Saat berbagi, setiap file mendapat kunci tautan baru; tautan disalin ke clipboard dan tetap aktif sampai Anda menghentikan Rintas atau mencabut semua tautan.

## Memakai aplikasi

1. Jalankan Rintas dari Start Menu. Aplikasi berjalan di system tray; startup otomatis aktif setelah aplikasi pertama kali dibuka. Klik kiri ikon tray atau buka Rintas lagi dari Start Menu untuk menampilkan dashboard.
2. Klik ikon Rintas di system tray lalu pilih **Bagikan file...** untuk memilih file. Pada installer EXE, Anda juga bisa klik kanan file lalu pilih **Bagikan dengan Rintas** (di Windows 11 saat ini lewat **Tampilkan opsi lainnya**).
3. Tautan disalin ke clipboard. Kirim tautan kepada penerima; mereka dapat mengunduh lewat browser.
4. Untuk mencabut semua tautan, gunakan menu tray **Hentikan semua tautan**. Pilih **Keluar** untuk mematikan server dan tunnel.

Dashboard dan submenu **Tautan aktif** di tray menampilkan tautan yang masih berlaku pada sesi server saat ini. Pilih tautan untuk menyalinnya lagi, atau cabut satu tautan dari dashboard. Di jendela **Pengaturan**, tombol **Panduan domain** menjelaskan cara menghubungkan subdomain melalui Cloudflare Tunnel.

Tautan baru memakai kunci acak 16 karakter, misalnya `https://domain-anda.example/AbCdEfGhIjKlMnOp`. Masa berlaku bawaan 24 jam; ubah melalui **Pengaturan domain dan masa berlaku** di menu tray (1–10080 menit). Saat masa berlaku habis, halaman dan unduhan tidak dapat dibuka lagi. Tautan juga berhenti bila Rintas dimatikan atau pengaturan disimpan ulang.

Laptop dan koneksi internet harus tetap menyala selama penerima mengunduh. Kecepatan unduh mengikuti kecepatan upload internet laptop. Siapa pun yang memperoleh tautan lengkap dapat mengunduh file sampai tautan dicabut.

## Domain

Secara bawaan Rintas membuat tautan sementara `trycloudflare.com`; alamat domain ini berubah saat tunnel dimulai ulang. Untuk alamat tetap, arahkan Cloudflare Named Tunnel ke `http://127.0.0.1:8765`, lalu isi domain HTTPS lewat menu tray **Pengaturan domain dan masa berlaku**. Mengisi domain tidak otomatis membuat rute Cloudflare. Rintas memeriksa rute tersebut sebelum mengizinkan tautan baru. Lihat [panduan domain tetap](docs/PANDUAN-DOMAIN-RINTAS.md), termasuk contoh `share.mfariz.me`.

## Paket Microsoft Store

Paket MSIX memakai identitas Partner Center `mfarzz.Rintas` dan Publisher `CN=324F93E6-C2FA-4F42-B623-76A85EC7F69D`. Bangun dengan `powershell -ExecutionPolicy Bypass -File .\msix\build-msix.ps1`; hasilnya `Rintas_1.0.9.0_x64.msix` di folder proyek. Microsoft Store menandatangani MSIX setelah sertifikasi. Paket ini menyertakan ekstensi `IExplorerCommand` agar **Bagikan dengan Rintas** muncul di menu utama Windows 11. Ekstensi telah lulus uji COM dasar dan manifest telah divalidasi saat pembuatan paket; perilakunya di Explorer belum diuji melalui instalasi paket bertanda tangan.

Untuk memasang paket secara langsung pada komputer pengujian, ikuti [panduan sertifikat MSIX](docs/PANDUAN-INSTAL-MSIX.md). Paket asli belum ditandatangani sehingga tidak dapat dipasang dengan klik dua kali. Paket uji lokal dan sertifikatnya adalah artefak mesin pengembang dan tidak disimpan dalam repo.

Ikon Explorer dan tray memakai logo pixel transparan dari folder `assets`; keduanya memilih versi terang atau gelap sesuai tema Windows. Halaman unduhan dan halaman tautan tidak aktif memakai PNG transparan `rintas-mark.png` pada tema gelap dan `rintas-mark-1.png` pada tema terang, dengan sudut tajam dan tanpa bayangan. Untuk membangun ulang ukuran ikon dari ekspor Figma, jalankan `python .\assets\build-icons.py` (memerlukan Pillow).

## Installer EXE

`FileShareSetup.exe` memasang Rintas untuk akun Windows, mendaftarkan menu klik kanan klasik, dan membuka Rintas di latar belakang. Installer memiliki kolom domain opsional. Aplikasi dapat dihapus dari **Settings > Apps > Installed apps**. Bangun ulang dengan `powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1`.

Installer EXE belum ditandatangani secara digital. Untuk distribusi publik, gunakan sertifikat Code Signing tepercaya dan jalankan `powershell -ExecutionPolicy Bypass -File .\installer\sign-installer.ps1 -Thumbprint THUMBPRINT_ANDA` setelah pembangunan ulang.

## Tanpa installer

Jalankan `Rintas.exe` dari folder ini. Untuk mendaftarkan menu klik kanan klasik, jalankan `powershell -ExecutionPolicy Bypass -File .\install-menu.ps1`; lepas dengan `.\uninstall-menu.ps1`. `cloudflared.exe` dan Node.js harus tersedia di folder ini atau di PATH.

## Struktur proyek

- `RintasApp.cs` — aplikasi Windows, tray, dashboard, pengaturan, dan menu berbagi.
- `share.js` — server lokal, tautan, masa berlaku, unduhan, dan Cloudflare Tunnel sementara.
- `assets/` — dua PNG logo transparan yang dipakai aplikasi dan `build-icons.py`; folder `generated/` dibuat ulang saat build.
- `msix/` — manifest, ekstensi menu Explorer, skrip build, dan pengujian.
- `installer/` — installer EXE dan skrip pemasangan.
- `docs/` — panduan domain, sertifikat uji, kebijakan privasi, konsep logo, dan varian ekspor logo yang tidak dipakai build.
- `config.json` — nilai bawaan; pengaturan pengguna yang sebenarnya disimpan di `%LOCALAPPDATA%\FileShareTunnel`.

Paket MSIX lama, installer EXE, `cloudflared.exe`, sertifikat uji, objek compiler, dan folder hasil pengujian tidak dimasukkan ke Git. Berkas tersebut tetap tersedia di laptop pengembang dan dibuat ulang atau diunduh saat dibutuhkan.

## Membangun dari source

Siapkan Windows x64, Node.js di `C:\Program Files\nodejs\node.exe`, Python dengan Pillow, Visual Studio C++ Build Tools, dan Windows SDK. Unduh `cloudflared.exe` Windows x64 dari [Cloudflare](https://developers.cloudflare.com/tunnel/downloads/) lalu letakkan di folder proyek. Skrip build saat ini memakai lokasi Visual Studio dan Windows SDK yang ditetapkan di bagian atas masing-masing skrip; sesuaikan jika versi yang terpasang berbeda.

```powershell
python .\assets\build-icons.py
powershell -ExecutionPolicy Bypass -File .\msix\build-msix.ps1
powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1
```

Pengujian server dapat dijalankan dengan `node .\msix\test-share.js` dan `node .\msix\test-domain.js`.
