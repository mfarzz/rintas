# Video promosi Rintas

Draf animasi vertikal 1080 × 1920, 30 fps, 21,5 detik. Dibuat dengan Remotion mengikuti skill `short-form-video` dan `launch-video` dari iart-ai. Semua karakter dan elemen antarmuka digambar dalam kode; logo penutup memakai aset Rintas asli. Tidak memerlukan Higgsfield.

## Jalankan

```powershell
cd promo
npm install
npm run still
npm run render
```

Hasil draf tanpa narasi berada di `promo/out/rintas-promo-draft.mp4`. Folder `out/` dan `node_modules/` diabaikan Git.
Skrip render memakai Google Chrome dari lokasi instalasi yang ada di laptop ini. Jika lokasinya berbeda di komputer lain, ubah opsi `--browser-executable` di `package.json`.

## Tambahkan suara ElevenLabs

Simpan audio hasil Text to Speech dengan nama `promo/public/voiceover.mp3`, lalu jalankan:

```powershell
npm run render:voice
```

Naskah TTS yang dirancang untuk draf ini:

> Masih unggah file setiap mau berbagi? Kenalan dengan Rintas. Pilih file di laptop, klik bagikan, dan tautannya langsung tersalin. Kirim link itu ke temanmu. Selama Rintas aktif dan tautannya belum kedaluwarsa, mereka bisa mengunduh lewat browser. Filenya tetap di laptopmu. Rintas. Berbagi file tanpa ribet.

Durasi audio ElevenLabs saat draf dibuat sekitar 20 detik. Jika audionya diganti, sesuaikan batas adegan di `src/video.jsx` sebelum render final. Caption per kata perlu dibuat dari timestamp audio asli sesuai skill `caption-animation`; draf ini hanya memakai teks judul per adegan.

Audio ElevenLabs milik Anda tidak dimasukkan ke Git. Draf ini menggambarkan antarmuka secara simbolis, bukan rekaman UI yang persis.
