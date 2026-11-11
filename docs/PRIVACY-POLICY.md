# Kebijakan Privasi Rintas

**Berlaku sejak:** 11 November 2026  
**Pengelola Rintas:** Muhammad Fariz
**Kontak privasi:** mfarix730@gmail.com

Rintas adalah aplikasi Windows untuk membagikan file dari komputer pengirim melalui tautan unduhan. Kebijakan ini menjelaskan data yang diproses saat seseorang menggunakan aplikasi Rintas atau membuka tautan berbagi yang dibuat dengannya.

## Data yang diproses dan tujuannya

Saat pengguna memilih file untuk dibagikan, Rintas membaca file tersebut dari komputer pengguna agar dapat menampilkan nama dan ukuran file serta mengirimkan isinya kepada orang yang membuka tautan unduhan. Rintas membuat kunci tautan acak dan menyimpan lokasi file serta waktu kedaluwarsa tautan dalam memori aplikasi selama sesi berjalan. Tautan disalin ke clipboard komputer pengirim.

Rintas menyimpan alamat domain yang diatur pengguna dan masa berlaku bawaan tautan dalam `config.json`, serta token yang dipakai aplikasi untuk mengendalikan server lokal dalam `control.token`. Aplikasi juga menulis `rintas.log` untuk mencatat waktu aktivitas, nama file yang dibagikan, jumlah sesi aktif, alamat tunnel, dan pesan galat. Berkas ini berada di folder `%LOCALAPPDATA%\FileShareTunnel` pada komputer pengguna. Nama file dapat mengandung informasi pribadi; pilih nama file dengan mempertimbangkan hal ini.

Ketika penerima membuka tautan, halaman unduhan menampilkan nama file, ukuran file, dan waktu kedaluwarsa. Jika penerima mengunduhnya, isi file dikirim dari komputer pengirim melalui jaringan. Versi Rintas saat ini tidak membuat akun pengguna, tidak menambahkan iklan atau analitik ke halaman unduhan, dan tidak memasang cookie melalui halaman tersebut. Server aplikasi tidak membuat log alamat IP penerima secara khusus.

## Alur jaringan dan pihak lain

Rintas menggunakan Cloudflare Tunnel untuk membuat server di komputer pengirim dapat diakses melalui alamat HTTPS. Permintaan penerima dan isi file yang diunduh melewati jaringan Cloudflare dalam perjalanan ke atau dari komputer pengirim. Rintas tidak mengunggah salinan file ke penyimpanan milik pengelola Rintas. Hal ini tidak berarti Cloudflare tidak memproses data lalu lintas: Cloudflare dapat memproses, dan dalam keadaan tertentu menyimpan, data jaringan atau konten sesuai layanan serta [Kebijakan Privasi Cloudflare](https://www.cloudflare.com/privacypolicy/). Pengguna yang mengatur domain sendiri juga perlu memperhatikan layanan DNS atau jaringan lain yang mereka gunakan.

Penerima yang memiliki tautan dapat melihat informasi file pada halaman unduhan dan mengambil isi file selama tautan aktif. Karena tautan berfungsi sebagai kunci akses, pengirim perlu membagikannya hanya kepada orang yang dituju. Rintas tidak mengirim daftar file, isi file, atau log lokal ke layanan analitik milik pengelola aplikasi.

Jika Rintas diperoleh melalui Microsoft Store, pemrosesan data oleh Microsoft untuk Store, pemasangan, dan pembaruan mengikuti [Pernyataan Privasi Microsoft](https://www.microsoft.com/id-id/privacy/privacystatement). Pemrosesan tersebut terpisah dari fungsi berbagi file Rintas.

## Penyimpanan, masa berlaku, dan penghapusan

File asli tetap berada di lokasi asal pada komputer pengirim. Rintas tidak membuat salinan tetap dari file tersebut. Informasi tautan aktif disimpan dalam memori dan hilang saat server Rintas berhenti. Tautan juga berhenti berlaku setelah masa berlaku yang dipilih pengguna, ketika pengguna memilih **Hentikan semua tautan**, atau ketika pengguna keluar dari aplikasi. Masa berlaku bawaan adalah 24 jam dan dapat diubah dari 1 menit hingga 7 hari.

Berkas pengaturan, token kontrol, dan log lokal tetap ada sampai pengguna menghapusnya atau aplikasi dihapus dengan cara yang menghapus folder data tersebut. Rintas saat ini tidak menghapus `rintas.log` secara otomatis. Rintas tidak dapat menghapus salinan file yang sudah diunduh penerima, riwayat browser penerima, atau data yang diproses oleh Cloudflare dan layanan pihak ketiga sesuai kebijakan mereka sendiri.

## Keamanan dan pilihan pengguna

Server file Rintas hanya mendengarkan pada komputer pengirim; akses dari internet diteruskan melalui Cloudflare Tunnel. Tautan memakai kunci acak, dan halaman unduhan mengirim header yang meminta browser tidak menyimpan halaman atau unduhan dalam cache. Namun, siapa pun yang memperoleh tautan lengkap dapat mengakses file sampai tautan berhenti berlaku. Jangan bagikan file sensitif melalui tautan kepada orang yang tidak dipercaya.

Pengirim mengendalikan file mana yang dibagikan, masa berlaku tautan baru, domain yang digunakan, serta kapan semua tautan dicabut atau Rintas dihentikan. Pengirim dapat membuka folder `%LOCALAPPDATA%\FileShareTunnel` untuk melihat atau menghapus pengaturan dan log lokal setelah Rintas ditutup. Jika Anda adalah penerima tautan dan ingin meminta akses, koreksi, atau penghapusan data yang dibagikan, hubungi terlebih dahulu orang yang mengirimkan tautan; pengelola Rintas tidak menyimpan salinan file di servernya. Untuk pertanyaan tentang pemrosesan oleh aplikasi, hubungi kontak privasi di atas.

## Perubahan kebijakan

Kebijakan ini dapat diperbarui ketika cara kerja Rintas berubah. Tanggal berlaku di bagian atas akan diperbarui saat perubahan diterbitkan. Versi terbaru akan tersedia melalui alamat kebijakan privasi yang dicantumkan pada halaman Rintas di Microsoft Store.
