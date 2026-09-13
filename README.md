# SmartLab Agent (Unimal) 🖥️⚡

> **Klien Desktop & Background Service Resmi untuk Sistem Manajemen Laboratorium Komputer Teknik Informatika Universitas Malikussaleh (Unimal)**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Status](https://img.shields.io/badge/Status-Production%20Ready-009344)

---

## 🌟 Ringkasan

**SmartLab Agent** adalah aplikasi desktop berbasis **.NET 8 (C#)** yang dipasang pada seluruh komputer laboratorium praktikum Unimal. Agent ini berjalan di latar belakang sebagai Windows Service dengan System Tray UI untuk monitoring real-time, eksekusi perintah remote dari dashboard, auto-update, inventarisasi hardware/software, pengawasan aplikasi/game terlarang, serta helpdesk pengaduan kerusakan meja.

### Fitur Lengkap Sistem (Fase 1 – 4):

- 🔗 **Zero-Config Pairing**: Pairing instan menggunakan 6-digit kode pairing dari Web Dashboard (mis. `UNM-REK01`).
- 💓 **Heartbeat Loop (15s)**: Pelaporan status berkala (online/offline, IP, MAC address, active user, uptime).
- ⚡ **Eksekusi Remote Terpusat**:
  - `shutdown`, `restart`, `abort_shutdown` (dengan hitung mundur grace period).
  - `broadcast` (Banner notifikasi melayang di layar mahasiswa dengan countdown timer).
  - `lock` (Penguncian workstation instan saat praktikum/ujian selesai).
  - `cleanup` (Pembersihan otomatis file sementara `%TEMP%` dan Recycle Bin).
  - `uninstall_software` (Silent uninstall software jarak jauh).
  - `kiosk_toggle` (Pembatasan Task Manager, CMD, Regedit, Control Panel via Windows Registry).
- 🔍 **Hardware & Disk Partition Scanner**: Pendataan otomatis spesifikasi CPU, RAM, OS, dan kapasitas partisi drive harddisk per PC.
- 📦 **Software Registry Inventory & Alert**: Sinkronisasi 100+ aplikasi Windows terpasang, pendeteksian otomatis bila ada mahasiswa menginstall software baru.
- 🚫 **Blocklist Process Watcher & Screenshot**: Pengawasan otomatis proses game terlarang (Valorant, Steam, Genshin, Cheat Engine, dsb). Jika terdeteksi dibuka, proses langsung dimatikan paksa (force-kill) dan screenshot layar otomatis diambil lalu diunggah ke dashboard.
- 🛠️ **Integrasi Lapor Kerusakan**: Menu klik kanan pada ikon tray: *"🛠 Lapor Kendala / Kerusakan PC Ini..."* yang langsung membuka form pelaporan kendala meja/PC di browser.
- ⏰ **Jadwal Lokal Mandiri**: Menjalankan jadwal shutdown otomatis secara lokal tanpa tergantung koneksi server saat waktu jatuh tempo.
- 🔄 **Auto-Update**: Terintegrasi otomatis dengan GitHub Releases (`MuslimGunawan/smartlab-agent`).

---

## 📁 Struktur Solusi (.NET 8)

```
lab-agent/
├── LabControl.sln
├── LabAgent.Shared/        # Class Library bersama (API Client, Config, Hardware/Software Manager, Blocklist, Kiosk, Scheduler)
├── LabAgent.Service/       # Windows Service (Background Worker, Heartbeat, Command Executor, Watcher)
├── LabAgent.Tray/          # Windows Forms Tray UI & Overlay Banner Broadcast
└── publish-agent.ps1       # Script otomatis build paket instalasi distribusi lab
```

---

## 🚀 Panduan Build & Pemasangan di PC Lab

### 1. Membuat Paket Instalasi (.bat)
Cukup jalankan script publish pada komputer pengembang:
```powershell
powershell -ExecutionPolicy Bypass -File .\publish-agent.ps1
```
Output paket siap pakai akan terbentuk di folder: `dist\SmartLab-Agent-Setup\` yang berisi:
- `install.bat` (Installer otomatis Administrator)
- `uninstall.bat` (Uninstaller bersih)
- `PANDUAN_INSTALLASI.txt`
- Folder `Service\` dan `Tray\`

### 2. Cara Pasang di PC Komputer Lab:
1. Salin folder `SmartLab-Agent-Setup` ke flashdisk atau network share.
2. Buka folder tersebut di PC Lab tujuan.
3. Klik kanan pada **`install.bat`** -> Pilih **"Run as administrator"**.
4. Installer akan otomatis mendaftarkan Windows Service, mendaftarkan autorun tray saat login, dan menanyakan Kode Pairing.
5. Masukkan Kode Pairing dari Dashboard (misal: `UNM-REK01`).
6. Selesai! PC akan langsung terhubung ke dashboard.

---

## 👨‍💻 Hak Cipta & Lisensi

Laboratorium Teknik Informatika  
Universitas Malikussaleh (Unimal), Aceh Utara, Indonesia.  
Dikembangkan untuk efisiensi dan keamanan operasional laboratorium komputer kampus.
