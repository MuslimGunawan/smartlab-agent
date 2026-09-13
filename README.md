# SmartLab Agent (Unimal) 🖥️⚡

> **Klien Desktop & Background Service untuk Sistem Manajemen Laboratorium Komputer Universitas Malikussaleh (Unimal)**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Status](https://img.shields.io/badge/Status-Fase%202%20Active-009344)

---

## 🌟 Ringkasan

**SmartLab Agent** adalah aplikasi desktop berbasis **.NET 8 (C#)** yang dipasang pada seluruh komputer laboratorium praktikum Unimal. Agent ini berjalan secara efisien di latar belakang sebagai Windows Service dengan System Tray UI untuk monitoring, eksekusi perintah remote dari dashboard, auto-update, dan penerapan kebijakan praktikum (Kiosk Mode).

### Fitur Utama

- 🔗 **Zero-Config Pairing**: Pairing instan menggunakan 6-digit OTP dari Web Dashboard.
- 💓 **Heartbeat Loop (10s)**: Pengiriman metrik berkala (RAM, CPU, IP, MAC, status aktif).
- ⚡ **Eksekusi Remote**:
  - `shutdown`, `restart`, `abort_shutdown` (dengan grace countdown period).
  - `broadcast` (Pesan peringatan / banner melayang di layar mahasiswa).
  - `kiosk_toggle` (Penguncian Task Manager, CMD, Regedit, Control Panel via Windows Registry).
- ⏰ **Jadwal Lokal Otomatis**: Toleran gangguan koneksi, menjalankan jadwal shutdown/kiosk otomatis secara lokal.
- 🔄 **Auto-Update**: Terintegrasi otomatis dengan GitHub Releases (`MuslimGunawan/smartlab-agent`).
- 🛡️ **Bypass Admin/ASLAB**: Asisten Lab dan Administrator tidak terkena restriksi Kiosk Mode.

---

## 📁 Struktur Solusi (.NET 8)

```
lab-agent/
├── LabControl.sln
├── LabAgent.Shared/        # Class Library bersama (API Client, Config, Models, Kiosk, Scheduler, Updater)
├── LabAgent.Service/       # Background Worker Service (Heartbeat, Command Executor, Local Scheduler)
└── LabAgent.Tray/          # WinForms Notification Tray UI & Banner Broadcast
```

---

## 🚀 Panduan Kompilasi & Menjalankan

### Persyaratan
- Windows 10 / 11 (x64)
- .NET 8.0 SDK

### Build Solusi
```powershell
dotnet build LabControl.sln -c Release
```

### Pairing Komputer Pertama Kali
Dapatkan kode pairing 6-digit dari dashboard Lab Unimal, lalu jalankan:
```powershell
dotnet run --project LabAgent.Service -- --pair 123456 --exit
```

### Menjalankan Service
```powershell
# Mode Produksi
dotnet run --project LabAgent.Service

# Mode Simulasi (Aman untuk dev tanpa mematikan komputer)
dotnet run --project LabAgent.Service -- --sim
```

### Menjalankan Tray UI
```powershell
dotnet run --project LabAgent.Tray
```

---

## 🤝 Kontributor & Hak Cipta
- **Pengembang**: Muslim Gunawan ([@MuslimGunawan](https://github.com/MuslimGunawan))
- **Institusi**: Laboratorium Komputer, Universitas Malikussaleh (Unimal)
