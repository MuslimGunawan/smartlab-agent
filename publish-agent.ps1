# Script Build & Packaging Production SmartLab Agent (Unimal)
# Output: dist\SmartLab-Agent-Setup

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $scriptDir "dist\SmartLab-Agent-Setup"

Write-Host "=================================================" -ForegroundColor Green
Write-Host "  Mempersiapkan Paket Distribusi SmartLab Agent  " -ForegroundColor Green
Write-Host "  Universitas Malikussaleh (Unimal)              " -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

if (Test-Path $outputDir) {
    Write-Host "Membersihkan folder output lama..." -ForegroundColor Yellow
    Remove-Item -Path $outputDir -Recurse -Force
}

$dotnetCmd = if (Test-Path "$HOME\.dotnet\dotnet.exe") { "$HOME\.dotnet\dotnet.exe" } else { "dotnet" }

Write-Host "`n[1/4] Mem-publish LabAgent.Service (Windows Service)..." -ForegroundColor Cyan
& $dotnetCmd publish (Join-Path $scriptDir "LabAgent.Service\LabAgent.Service.csproj") -c Release -r win-x64 --self-contained false -o (Join-Path $outputDir "Service")

Write-Host "`n[2/4] Mem-publish LabAgent.Tray (User Session UI)..." -ForegroundColor Cyan
& $dotnetCmd publish (Join-Path $scriptDir "LabAgent.Tray\LabAgent.Tray.csproj") -c Release -r win-x64 --self-contained false -o (Join-Path $outputDir "Tray")

Write-Host "`n[3/4] Membuat Script Installer & Uninstaller..." -ForegroundColor Cyan

# 1. install.bat
$installBatContent = @'
@echo off
chcp 65001 >nul
cls
echo =========================================================
echo    INSTALLER SMARTLAB AGENT — TEKNIK INFORMATIKA UNIMAL
echo =========================================================
echo.

:: Cek Hak Akses Administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Script ini HARUS dijalankan sebagai Administrator!
    echo Klik kanan file install.bat lalu pilih "Run as administrator".
    echo.
    pause
    exit /b 1
)

set "INSTALL_DIR=C:\Program Files\SmartLab Agent"
echo [*] Lokasi instalasi: %INSTALL_DIR%
echo.

:: Hentikan service jika sedang berjalan
sc query SmartLabAgent >nul 2>&1
if %errorLevel% equ 0 (
    echo [*] Menghentikan service lama...
    net stop SmartLabAgent >nul 2>&1
    sc delete SmartLabAgent >nul 2>&1
    timeout /t 2 >nul
)

:: Buat folder instalasi
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

echo [*] Menyalin file service dan aplikasi...
xcopy /E /I /Y "%~dp0Service" "%INSTALL_DIR%\Service" >nul
xcopy /E /I /Y "%~dp0Tray" "%INSTALL_DIR%\Tray" >nul

:: Daftarkan Windows Service
echo [*] Mendaftarkan Windows Service (SmartLabAgent)...
sc create "SmartLabAgent" binPath= "\"%INSTALL_DIR%\Service\LabAgent.Service.exe\"" start= auto DisplayName= "SmartLab Agent Service (Unimal)" >nul
sc description "SmartLabAgent" "Layanan background manajemen dan monitoring komputer laboratorium Teknik Informatika Universitas Malikussaleh." >nul

:: Jalankan Windows Service
echo [*] Menjalankan service...
net start SmartLabAgent

:: Daftarkan Tray di Startup Windows (HKLM Run)
echo [*] Mendaftarkan Tray UI pada Startup Windows...
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "SmartLabTray" /t REG_SZ /d "\"%INSTALL_DIR%\Tray\LabAgent.Tray.exe\"" /f >nul

:: Jalankan Tray untuk sesi saat ini
echo [*] Meluncurkan ikon Tray...
start "" "%INSTALL_DIR%\Tray\LabAgent.Tray.exe"

echo.
echo =========================================================
echo     INSTALASI PROGRAM BERHASIL DISELESAIKAN!
echo =========================================================
echo.
set /p PAIR_NOW="Apakah ingin langsung menghubungkan PC ini dengan Kode Pairing? (Y/N): "
if /i "%PAIR_NOW%"=="Y" (
    echo.
    set /p CODE="Masukkan Kode Pairing dari Dashboard ASLAB (contoh: UNM-REK01): "
    if not "%CODE%"=="" (
        echo [*] Menghubungkan ke server...
        "%INSTALL_DIR%\Service\LabAgent.Service.exe" --pair %CODE% --exit
    )
)

echo.
echo [SELESAI] SmartLab Agent siap digunakan di laboratorium.
echo Tekan tombol apa saja untuk menutup...
pause >nul
'@
[System.IO.File]::WriteAllText((Join-Path $outputDir "install.bat"), $installBatContent, [System.Text.Encoding]::UTF8)

# 2. uninstall.bat
$uninstallBatContent = @'
@echo off
chcp 65001 >nul
cls
echo =========================================================
echo    UNINSTALLER SMARTLAB AGENT — TEKNIK INFORMATIKA UNIMAL
echo =========================================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Script ini HARUS dijalankan sebagai Administrator!
    echo Klik kanan file uninstall.bat lalu pilih "Run as administrator".
    echo.
    pause
    exit /b 1
)

set /p CONFIRM="Apakah Anda yakin ingin MENGHAPUS SmartLab Agent dari PC ini? (Y/N): "
if /i "%CONFIRM%" neq "Y" (
    echo Pembatalan dilakukan.
    exit /b 0
)

echo [*] Menghentikan proses Tray...
taskkill /F /IM LabAgent.Tray.exe >nul 2>&1

echo [*] Menghentikan dan menghapus Windows Service...
net stop SmartLabAgent >nul 2>&1
sc delete SmartLabAgent >nul 2>&1

echo [*] Menghapus registrasi startup...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v "SmartLabTray" /f >nul 2>&1

echo [*] Menghapus folder instalasi...
rmdir /S /Q "C:\Program Files\SmartLab Agent" >nul 2>&1

echo.
set /p DEL_CONFIG="Hapus juga file konfigurasi pairing lokal di C:\ProgramData\LabControl? (Y/N): "
if /i "%DEL_CONFIG%"=="Y" (
    rmdir /S /Q "C:\ProgramData\LabControl" >nul 2>&1
    echo [*] Konfigurasi lokal dibersihkan.
)

echo.
echo [SELESAI] SmartLab Agent telah berhasil dihapus sepenuhnya.
pause
'@
[System.IO.File]::WriteAllText((Join-Path $outputDir "uninstall.bat"), $uninstallBatContent, [System.Text.Encoding]::UTF8)

# 3. PANDUAN_INSTALLASI.txt
$readmeContent = @"
=============================================================================
  PANDUAN PEMASANGAN (INSTALLATION GUIDE) SMARTLAB AGENT
  Laboratorium Teknik Informatika — Universitas Malikussaleh (Unimal)
=============================================================================

Persyaratan Sistem PC Lab:
- Windows 10 atau Windows 11 (64-bit)
- Microsoft .NET 8.0 Runtime (Desktop / Windows Desktop Runtime)
- Terkoneksi ke jaringan kampus / lab (dapat mengakses server dashboard)

Langkah-Langkah Instalasi di PC Lab:

1. Buka folder ini di PC Lab tujuan (bisa melalui flashdisk atau shared folder).
2. Klik kanan pada file 'install.bat' -> Pilih "Run as administrator".
3. Installer akan secara otomatis:
   - Membuat direktori 'C:\Program Files\SmartLab Agent'
   - Memasang Windows Service 'SmartLabAgent' (berjalan otomatis di background)
   - Mendaftarkan Tray UI di Windows Startup (muncul di pojok kanan bawah desktop)
   - Menjalankan service & tray icon
4. Saat installer bertanya "Apakah ingin langsung menghubungkan PC ini dengan Kode Pairing? (Y/N)",
   ketik Y lalu masukkan Kode Pairing yang didapat dari Dashboard ASLAB
   (Contoh: UNM-REK01).
5. Selesai! PC lab akan langsung muncul di Dashboard SmartLab dengan status ONLINE,
   lengkap dengan spesifikasi CPU, RAM, partisi harddisk, dan daftar software.

Menu Pengguna pada PC Lab:
- Di pojok kanan bawah (system tray), mahasiswa/aslab dapat melihat ikon monitor hijau.
- Klik kanan ikon tray untuk:
  * "🛠 Lapor Kendala / Kerusakan PC Ini..." -> Membuka form lapor masalah
  * "Informasi & Status PC" -> Melihat lab tempat PC terdaftar
  * "Pasangkan PC ke Ruangan Lab..." -> Memasukkan kode pairing baru jika diperlukan

Cara Menghapus (Uninstall):
- Klik kanan pada 'uninstall.bat' -> Pilih "Run as administrator".
"@
[System.IO.File]::WriteAllText((Join-Path $outputDir "PANDUAN_INSTALLASI.txt"), $readmeContent, [System.Text.Encoding]::UTF8)

Write-Host "`n[4/4] Paket Setup Siap Digunakan di: $outputDir" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
