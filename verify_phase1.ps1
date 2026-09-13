# 1. Create a fresh lab and pairing code
$labId = (& mysql -u root -N -e "SELECT id FROM labcontrol_unimal.labs ORDER BY id ASC LIMIT 1;").Trim()
$code = "UNM-TEST" + (Get-Random -Minimum 1000 -Maximum 9999)

$sql = "INSERT INTO labcontrol_unimal.pairing_codes (code, lab_id, is_used, expired_at, created_at, updated_at) VALUES ('$code', $labId, 0, DATE_ADD(NOW(), INTERVAL 1 DAY), NOW(), NOW());"
& mysql -u root -e "$sql"

Write-Host ">>> Generated Pairing Code for Lab #${labId}: $code"

# 2. Run LabAgent.Service with --pair and --sim
$dotnetExe = "$HOME\.dotnet\dotnet.exe"
$serviceDll = "c:\laragon\www\Aplikasi Desktop\ASLAB\lab-agent\LabAgent.Service\bin\Debug\net8.0\LabAgent.Service.dll"

Write-Host ">>> Running Agent CLI Pairing..."
$pairOutput = & $dotnetExe $serviceDll --pair $code --sim --url "http://localhost:8000/api/v1" --exit
Write-Host $pairOutput

# 3. Check Computer record in DB
$computerRecord = & mysql -u root -N -e "SELECT id, nama_pc, device_token, status FROM labcontrol_unimal.computers WHERE nama_pc LIKE '%$env:COMPUTERNAME%' ORDER BY id DESC LIMIT 1;"
Write-Host ">>> Registered Computer in DB: $computerRecord"

$parts = $computerRecord.Split("`t")
$computerId = $parts[0].Trim()

# 4. Enqueue a Restart Command via Dashboard/Database
Write-Host ">>> ASLAB Dispatching Restart Command for Computer #$computerId..."
$cmdSql = "INSERT INTO labcontrol_unimal.commands (computer_id, lab_id, tipe, payload, status, created_at, updated_at) VALUES ($computerId, $labId, 'restart', '{\`"grace_seconds\`": 60, \`"message\`": \`"Uji Coba Perintah dari ASLAB TI Unimal\`"}', 'pending', NOW(), NOW());"
& mysql -u root -e "$cmdSql"

# 5. Start Agent Service in Background for 20 seconds to perform heartbeat cycle & command execution
Write-Host ">>> Starting Agent Service Worker for 20 seconds..."
$process = Start-Process -FilePath $dotnetExe -ArgumentList "`"$serviceDll`"", "--sim" -PassThru -NoNewWindow
Start-Sleep -Seconds 18
if (!$process.HasExited) {
    Stop-Process -Id $process.Id -Force
}

# 6. Verify Command execution in DB
$commandStatus = & mysql -u root -N -e "SELECT id, tipe, status, result_message, executed_at FROM labcontrol_unimal.commands WHERE computer_id = $computerId ORDER BY id DESC LIMIT 1;"
Write-Host ">>> Final Command Status in DB: $commandStatus"

# 7. Check Dashboard Status Feed
$feed = Invoke-RestMethod -Uri "http://127.0.0.1:8000/dashboard/status-feed" -Method Get
Write-Host ">>> Total Online in Dashboard Feed: $($feed.data.total_online)"
$pairedPc = $feed.data.computers | Where-Object { $_.id -eq [int]$computerId }
Write-Host ">>> Paired PC in Dashboard: Name=$($pairedPc.nama_pc), Status=$($pairedPc.status), Lab=$($pairedPc.lab_nama)"
