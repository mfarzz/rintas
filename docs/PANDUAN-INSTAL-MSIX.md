# Mengatasi galat sertifikat saat memasang Rintas MSIX

Pesan **â€œThis app package's publisher certificate could not be verified”** dengan kode `0x800B010A` berarti Windows tidak dapat membangun rantai sertifikat penanda tangan paket ke penerbit yang dipercaya. Pada paket Rintas di folder ini, pemeriksaan `Get-AuthenticodeSignature` menunjukkan **`NotSigned`**. Jadi paket tersebut memang belum siap dipasang dengan klik dua kali.

Jika Anda sudah memasang sertifikat uji Rintas sebelumnya, gunakan **`Rintas_1.0.9.0_x64-local-test.msix`** yang telah ditandatangani untuk pembaruan lokal. Langkah berikut diperlukan bila Anda membangun paket uji sendiri atau menguji pada komputer lain.

## Untuk menguji Rintas di laptop sendiri

Langkah di bawah membuat sertifikat pengembangan, memasang **sertifikat publiknya** ke Windows, lalu menandatangani **salinan** paket. Jalankan **Windows PowerShell sebagai Administrator**. Gunakan hanya pada komputer pengujian milik Anda.

```powershell
Set-Location 'C:\lokasi\folder\rintas'

# Nilai ini harus persis sama dengan Publisher di msix\AppxManifest.xml.
$publisher = 'CN=324F93E6-C2FA-4F42-B623-76A85EC7F69D'
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -KeyUsage DigitalSignature `
  -Subject $publisher `
  -CertStoreLocation 'Cert:\CurrentUser\My' `
  -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}') `
  -FriendlyName 'Rintas MSIX Local Test'

# Ekspor hanya sertifikat publik (.cer), bukan kunci privat.
Export-Certificate -Cert $cert -FilePath '.\Rintas-local-test.cer' | Out-Null
Import-Certificate `
  -FilePath '.\Rintas-local-test.cer' `
  -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

# Simpan paket asli untuk unggahan Store; tanda tangani salinan untuk uji lokal.
$testPackage = '.\Rintas_1.0.9.0_x64-local-test.msix'
Copy-Item '.\Rintas_1.0.9.0_x64.msix' $testPackage -Force
$signTool = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe'
& $signTool sign /fd SHA256 /sha1 $cert.Thumbprint /s My /v $testPackage

# Pastikan Status = Valid sebelum memasang.
Get-AuthenticodeSignature $testPackage | Format-List Status,StatusMessage,SignerCertificate
Add-AppxPackage -Path $testPackage
```

Jika lokasi `signtool.exe` berbeda, cari versi x64 yang terpasang di `C:\Program Files (x86)\Windows Kits\10\bin\` dan ubah nilai `$signTool`. Jika perintah penandatanganan gagal, **jangan lanjutkan** ke `Add-AppxPackage`; baca pesan kesalahannya terlebih dahulu.

Sertifikat pengembangan ini hanya dipercaya oleh komputer tempat sertifikat publiknya dipasang. Jangan membagikan kunci privat atau sertifikat `.pfx` kepada pengguna lain. Untuk penguji lain, mereka harus memasang berkas `.cer` ke **Local Computer â†’ Trusted People** sebelum memasang paket uji yang telah ditandatangani.

## Untuk Microsoft Partner Center

Unggah paket asli **`Rintas_1.0.9.0_x64.msix`**, bukan salinan `-local-test.msix`. Untuk distribusi lewat Microsoft Store, Microsoft menandatangani ulang MSIX setelah paket lolos sertifikasi. Sertifikat pengembangan di atas hanya untuk pengujian pemasangan langsung di luar Store.

## Jika masih gagal

1. Periksa paket yang benar: `Get-AuthenticodeSignature '.\Rintas_1.0.9.0_x64-local-test.msix'`. `NotSigned` berarti langkah `signtool sign` belum berhasil; `Valid` berarti tanda tangan dapat diverifikasi pada komputer ini.
2. Pastikan sertifikat dengan thumbprint yang sama ada di `Cert:\LocalMachine\TrustedPeople`: `Get-ChildItem 'Cert:\LocalMachine\TrustedPeople' | Where-Object Thumbprint -eq $cert.Thumbprint`. Penyimpanan **CurrentUser** saja tidak cukup untuk App Installer.
3. Jika muncul galat penerbit tidak cocok, pastikan subject sertifikat persis `CN=324F93E6-C2FA-4F42-B623-76A85EC7F69D` dan tidak mengubah identitas di manifest setelah paket dibangun.
4. Jika pernah memasang Rintas dengan sertifikat lain, hapus versi uji yang lama dari **Settings â†’ Apps â†’ Installed apps**, lalu coba paket uji terbaru.

Rujukan resmi: [panduan penandatanganan MSIX](https://learn.microsoft.com/windows/msix/package/sign-msix-package-guide), [pemecahan masalah tanda tangan paket](https://learn.microsoft.com/windows/win32/appxpkg/how-to-troubleshoot-app-package-signature-errors), dan [persyaratan paket Microsoft Store](https://learn.microsoft.com/windows/apps/publish/publish-your-app/package-version-numbering).
