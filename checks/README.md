# Pemeriksaan proyek

Input dan batas sumber perintah native:

```powershell
dotnet run --project checks/InputChecks/InputChecks.csproj
```

UI HTML/CSS/JavaScript (memerlukan Node.js dan Playwright):

```powershell
npm install --no-save playwright
npx playwright install chromium
node checks/ui-checks.cjs
```

UI memakai bridge native tiruan agar tombol, pesan, tema, pencarian, dan
penanganan teks tidak tepercaya dapat diperiksa secara terpisah. Ini tidak
menjalankan host Windows atau WebView2. Pratinjau adalah render antarmuka asli
dengan contoh tab dan bridge tiruan; bukan screenshot aplikasi native Windows.
Pengujian di Windows tetap diperlukan untuk lifecycle WebView2, keyboard,
login website, download, izin perangkat, dan pemulihan sesi.
