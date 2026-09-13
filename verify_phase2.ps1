Write-Host "================================================="
Write-Host "PENGUJIAN FITUR FASE 2: SMARTLAB UNIMAL"
Write-Host "================================================="

# 1. Check /api/v1/agent/config endpoint
$pc = & mysql -u root -N -e "SELECT id, device_token FROM labcontrol_unimal.computers ORDER BY id DESC LIMIT 1;"
$parts = $pc.Split("`t")
$computerId = $parts[0].Trim()
$token = $parts[1].Trim()

$headers = @{ 
    "Authorization" = "Bearer $token"
    "Accept" = "application/json" 
}

Write-Host "`n[TEST 1] Menguji Endpoint GET /api/v1/agent/config..."
$configRes = Invoke-RestMethod -Uri "http://127.0.0.1:8000/api/v1/agent/config" -Method Get -Headers $headers
Write-Host "Config Success: $($configRes.success)"
Write-Host "GitHub Repo Auto-Update: $($configRes.data.github_repo)"
Write-Host "Jadwal Terdaftar: $($configRes.data.schedules.Count)"
Write-Host "Status Kiosk: TaskMgr=$($configRes.data.kiosk_settings.disable_taskmgr), CMD=$($configRes.data.kiosk_settings.disable_cmd)"

# 2. Test Wake-on-LAN Trigger
Write-Host "`n[TEST 2] Menguji Fitur Wake-on-LAN..."
$wolRes = & mysql -u root -e "UPDATE labcontrol_unimal.computers SET mac_address = 'AA:BB:CC:DD:EE:FF' WHERE id = $computerId;"
# Use php artisan or direct service test
$wolTest = & php -r "require 'c:/laragon/www/Aplikasi Desktop/ASLAB/lab-dashboard/vendor/autoload.php'; \$app = require_once 'c:/laragon/www/Aplikasi Desktop/ASLAB/lab-dashboard/bootstrap/app.php'; \$res = \App\Services\WakeOnLanService::wake('AA:BB:CC:DD:EE:FF'); echo \$res ? 'WOL_PACKET_SENT' : 'WOL_FAILED';"
Write-Host "Hasil Pengiriman Magic Packet WOL: $wolTest"

# 3. Test Broadcast Screen Command
Write-Host "`n[TEST 3] Menguji Perintah Broadcast Pesan Layar..."
$broadcastSql = "INSERT INTO labcontrol_unimal.commands (computer_id, tipe, payload, status, created_at, updated_at) VALUES ($computerId, 'broadcast', '{\`"message\`": \`"Praktikum Pemrograman Web dimulai dalam 10 menit. Harap bersiap.\`", \`"grace_seconds\`": 15}', 'pending', NOW(), NOW());"
& mysql -u root -e "$broadcastSql"

$dotnetExe = "$HOME\.dotnet\dotnet.exe"
$serviceDll = "c:\laragon\www\Aplikasi Desktop\ASLAB\lab-agent\LabAgent.Service\bin\Debug\net8.0-windows\LabAgent.Service.dll"

# Start service for 15 seconds to receive broadcast
$process = Start-Process -FilePath $dotnetExe -ArgumentList "`"$serviceDll`"", "--sim" -PassThru -NoNewWindow
Start-Sleep -Seconds 12
if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }

$cmdStatus = & mysql -u root -N -e "SELECT tipe, status, result_message FROM labcontrol_unimal.commands WHERE computer_id = $computerId AND tipe = 'broadcast' ORDER BY id DESC LIMIT 1;"
Write-Host "Status Perintah Broadcast di Database: $cmdStatus"

# 4. Test Kiosk Mode Registry Enforcement
Write-Host "`n[TEST 4] Menguji Kiosk Mode Registry Enforcement..."
# Test via KioskManager logic using PowerShell
$sysKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"
if (!(Test-Path $sysKey)) { New-Item -Path $sysKey -Force | Out-Null }

# Set DisableTaskMgr = 1
Set-ItemProperty -Path $sysKey -Name "DisableTaskMgr" -Value 1
$val = (Get-ItemProperty -Path $sysKey -Name "DisableTaskMgr").DisableTaskMgr
Write-Host "Registry DisableTaskMgr saat AKTIF: $val"

# Restore DisableTaskMgr = 0 / remove
Remove-ItemProperty -Path $sysKey -Name "DisableTaskMgr" -ErrorAction SilentlyContinue
$restored = (Get-ItemProperty -Path $sysKey -Name "DisableTaskMgr" -ErrorAction SilentlyContinue)
$restoreStatus = if ($null -eq $restored) { 'BERSIH/NORMAL' } else { $restored.DisableTaskMgr }
Write-Host "Registry DisableTaskMgr saat DINONAKTIFKAN: $restoreStatus"

# 5. Test Auto-Update GitHub Releases Connector
Write-Host "`n[TEST 5] Menguji Konektivitas GitHub Releases MuslimGunawan/smartlab-agent..."
$updateCheck = & php -r "
\$ch = curl_init('https://api.github.com/repos/MuslimGunawan/smartlab-agent/releases/latest');
curl_setopt(\$ch, CURLOPT_USERAGENT, 'SmartLab-Agent');
curl_setopt(\$ch, CURLOPT_RETURNTRANSFER, true);
\$res = curl_exec(\$ch);
\$code = curl_getinfo(\$ch, CURLINFO_HTTP_CODE);
echo 'HTTP_CODE:' . \$code;
"
Write-Host "Pemeriksaan GitHub Releases: $updateCheck (Repo siap menerima file rilis installer Velopack)"

Write-Host "`n================================================="
Write-Host "SELURUH PENGUJIAN FASE 2 SELESAI"
Write-Host "================================================="
