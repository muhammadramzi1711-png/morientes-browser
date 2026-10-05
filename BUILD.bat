@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK belum tersedia. Pasang dari https://dotnet.microsoft.com/download
  pause
  exit /b 1
)
dotnet restore MorientesBrowser\MorientesBrowser.csproj --locked-mode
if errorlevel 1 (
  echo Pemulihan dependensi gagal. Lihat pesan di atas.
  pause
  exit /b 1
)
dotnet build MorientesBrowser\MorientesBrowser.csproj -c Release --no-restore
if errorlevel 1 (
  echo Build gagal. Lihat pesan di atas.
  pause
  exit /b 1
)
echo Berhasil. Aplikasi ada di MorientesBrowser\bin\Release\net48
pause
