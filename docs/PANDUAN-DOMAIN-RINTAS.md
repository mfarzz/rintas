# Menghubungkan domain tetap ke Rintas

Mengisi **Domain HTTPS** di Rintas hanya menentukan alamat tautan yang disalin. Domain juga harus memiliki rute Cloudflare Tunnel menuju server Rintas di laptop. Tanpa rute itu, alamat dapat membuka situs lain, seperti GitHub Pages, walaupun tautan berisi kunci Rintas.

## Untuk `mfariz.me`

Saat diperiksa pada 11 November 2026, `mfariz.me` memakai nameserver Cloudflare dan tautan dari domain utama menampilkan halaman GitHub Pages. Biarkan domain utama untuk situs tersebut. Gunakan subdomain terpisah, misalnya **`share.mfariz.me`**, untuk Rintas.

1. Masuk ke [Cloudflare Dashboard](https://dash.cloudflare.com/) dan buka **Networking → Tunnels**. Buat tunnel baru bernama `rintas`.
2. Pilih konektor **Windows**. Ikuti perintah pemasangan `cloudflared` dari Cloudflare pada laptop yang menjalankan Rintas. Jalankan sebagai layanan Windows agar tunnel tetap berjalan di latar belakang. Simpan token tunnel secara privat; jangan masukkan ke dokumen atau kirim lewat chat.
3. Pada tunnel tersebut, tambahkan **Published application** dengan hostname `share.mfariz.me` dan Service URL **`http://127.0.0.1:8765`**. Gunakan protokol HTTP untuk koneksi dari tunnel ke server lokal. Cloudflare akan mengatur rute DNS untuk hostname itu.
4. Pastikan layanan tunnel tersambung. Di Rintas, buka **Pengaturan**, isi **`https://share.mfariz.me`** (termasuk `//`), lalu simpan. Rintas akan memeriksa bahwa alamat publik benar-benar mencapai server lokal sebelum membuat tautan baru.
5. Buka `https://share.mfariz.me/` di browser. Respons **“Tautan tidak ditemukan”** dari Rintas berarti rute sudah mencapai aplikasi. Setelah itu buat tautan baru dan coba dari perangkat lain. Tautan lama yang berawalan `https://mfariz.me` perlu dibuat ulang.

Jika belum ingin menyiapkan tunnel tetap, kosongkan kolom domain di Rintas. Aplikasi akan menggunakan alamat sementara `trycloudflare.com`. Alamat sementara berubah ketika tunnel dimulai ulang.

Laptop, server Rintas, dan konektor tunnel harus tetap menyala selama tautan digunakan. File tetap dibaca dari laptop; lalu lintas unduhan melewati Cloudflare Tunnel. Mengubah DNS domain utama `mfariz.me` dapat memutus situs yang sekarang ada di sana, sehingga gunakan subdomain untuk berbagi file.

Jika domain baru sudah ditemukan oleh DNS publik tetapi jaringan laptop masih menjawab `ENOTFOUND`, tunggu cache DNS jaringan kedaluwarsa atau gunakan DNS lain pada jaringan tersebut. Sejak versi 1.0.8.0, Rintas mencoba resolver publik hanya untuk pemeriksaan hostname ketika resolver lokal menjawab `ENOTFOUND` atau `EAI_AGAIN`. Pengaturan DNS Windows tidak diubah.

Rujukan: [Menyiapkan Cloudflare Tunnel](https://developers.cloudflare.com/tunnel/get-started/), [Rute hostname dan DNS](https://developers.cloudflare.com/tunnel/concepts/routing/), [Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/).
