# Morientes Browser

Browser pribadi Windows 10/11 x64 dengan tampilan ala Safari. Aplikasi dan antarmuka dibuat sendiri, dengan Microsoft WebView2 sebagai mesin web.

## Instalasi sekali

Buka [Releases](https://github.com/muhammadramzi1711-png/morientes-browser/releases/latest), unduh file **Setup.exe**, tutup salinan ZIP lama, lalu jalankan installer. Selanjutnya buka Morientes melalui shortcut yang dibuat installer. Jangan terus menjalankan EXE dari folder ZIP lama: salinan itu tidak memiliki updater.

Installer memeriksa .NET Framework 4.8 dan WebView2 Runtime. Aplikasi belum ditandatangani dengan sertifikat Authenticode komersial.

## Pembaruan berikutnya

Saat dibuka dan setiap jam, aplikasi memeriksa rilis publik repo ini. Versi lebih baru diunduh di latar belakang menggunakan Velopack 1.2.161, yang memeriksa hash dan ukuran paket. Pembaruan yang selesai diunduh diterapkan saat browser dibuka kembali. Browser tidak direstart paksa di tengah sesi.

Di **Pengaturan → Pembaruan browser** tersedia tombol **Cek pembaruan**, status unduhan, dan pilihan menonaktifkan unduhan otomatis. Kegagalan jaringan, checksum atau penguncian paket tidak menghentikan browsing. Aplikasi tidak menyimpan token GitHub.

Bookmark, riwayat dan pengaturan memakai profil yang sama dengan versi ZIP: `%LOCALAPPDATA%\MorientesBrowser`. Cookie/login WebView2 ada di subfolder `WebProfile`. Installer memakai folder aplikasi terpisah, sehingga pembaruan tidak mengganti profil pengguna. Jangan memasukkan data profil ke repo.

## Rilis otomatis

Perubahan ke kode browser pada `main` memicu `.github/workflows/release-windows.yml`. GitHub Actions menjalankan pemeriksaan input, perilaku updater, UI; membuild aplikasi pada runner Windows; membuat installer serta feed pembaruan; lalu menerbitkan GitHub Release. Kegagalan sebelum langkah publikasi menghentikan rilis. Token rilis hanya tersedia di runner melalui `GITHUB_TOKEN`, tidak ikut paket aplikasi.

Versi rilis `0.2.<nomor-run>` bertambah otomatis. Push yang hanya mengubah README tidak membuat rilis. Menjalankan ulang run yang sudah dirilis tidak menimpa tag; gunakan **Run workflow** untuk nomor-run baru.

## Pengembangan

- .NET SDK 8, .NET Framework reference assemblies lewat NuGet.
- `dotnet restore MorientesBrowser/MorientesBrowser.csproj --locked-mode`
- `dotnet build MorientesBrowser/MorientesBrowser.csproj -c Release --no-restore`
- `dotnet run --project checks/InputChecks/InputChecks.csproj`
- `dotnet run --project checks/UpdateChecks/UpdateChecks.csproj`
- `npm ci`, `npx playwright install chromium`, `npm run test:ui`

Versi macOS adalah proyek source terpisah; pipeline ini hanya merilis Windows. Ini aplikasi preview: pemeriksaan otomatis bukan pengganti uji instalasi, update antarversi, dan startup di PC pengguna.
