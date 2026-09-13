using LabAgent.Shared.Models;
using LabAgent.Shared.Services;

namespace LabAgent.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ApiClient _apiClient;
        private readonly AgentConfigManager _configManager;
        private readonly CommandExecutor _commandExecutor;
        private readonly KioskManager _kioskManager;
        private readonly ScheduleManager _scheduleManager;
        private readonly UpdateManager _updateManager;
        private readonly HardwareManager _hardwareManager;
        private readonly SoftwareManager _softwareManager;
        private readonly BlocklistWatcher _blocklistWatcher;
        private readonly OfflineQueueManager _offlineQueueManager;

        private DateTime _lastConfigSync = DateTime.MinValue;
        private DateTime _lastUpdateCheck = DateTime.MinValue;
        private DateTime _lastHardwareSync = DateTime.MinValue;
        private DateTime _lastSoftwareSync = DateTime.MinValue;

        public Worker(
            ILogger<Worker> logger,
            ApiClient apiClient,
            AgentConfigManager configManager,
            CommandExecutor commandExecutor)
        {
            _logger = logger;
            _apiClient = apiClient;
            _configManager = configManager;
            _commandExecutor = commandExecutor;
            _kioskManager = new KioskManager();
            _scheduleManager = new ScheduleManager();
            _hardwareManager = new HardwareManager();
            _softwareManager = new SoftwareManager();
            _blocklistWatcher = new BlocklistWatcher();
            _offlineQueueManager = new OfflineQueueManager();

            var cfg = _configManager.Load();
            _updateManager = new UpdateManager("MuslimGunawan/smartlab-agent");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=================================================");
            _logger.LogInformation("SmartLab Unimal Agent Service Dimulai (Production Ready)");
            _logger.LogInformation("Repo Auto-Update: https://github.com/MuslimGunawan/smartlab-agent");
            _logger.LogInformation("=================================================");

            var config = _configManager.Load();
            _logger.LogInformation("Server URL: {Url}", config.ServerBaseUrl);
            _logger.LogInformation("Status Pairing: {Status}", _configManager.IsPaired(config) ? $"Terhubung (PC #{config.ComputerId} - {config.ComputerName})" : "Belum Dipasangkan");
            _logger.LogInformation("Simulation Mode: {Sim}", config.IsSimulationMode ? "AKTIF (Aman untuk dev/testing)" : "NONAKTIF (Perintah mematikan PC sungguhan)");

            while (!stoppingToken.IsCancellationRequested)
            {
                config = _configManager.Load();

                if (!_configManager.IsPaired(config))
                {
                    _logger.LogWarning("Agent belum dipasangkan dengan Ruangan Lab. Harap pasangkan via GUI Tray atau gunakan kode pairing.");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                try
                {
                    // 1. Sync Config, Schedules & Blocklist every 5 minutes or at startup
                    if ((DateTime.UtcNow - _lastConfigSync).TotalMinutes >= 5)
                    {
                        var configRes = await _apiClient.GetConfigAsync();
                        if (configRes != null && configRes.Success && configRes.Data != null)
                        {
                            _lastConfigSync = DateTime.UtcNow;
                            _scheduleManager.UpdateSchedules(configRes.Data.Schedules);
                            _blocklistWatcher.UpdateBlocklist(configRes.Data.Blocklist);
                            _logger.LogInformation("Sinkronisasi konfigurasi berhasil: {SchedCount} jadwal, {BlockCount} aplikasi terlarang termuat.",
                                configRes.Data.Schedules.Count, configRes.Data.Blocklist.Count);

                            // Apply Kiosk settings if defined
                            if (configRes.Data.KioskSettings != null)
                            {
                                _kioskManager.ApplyPolicy(configRes.Data.KioskSettings);
                            }
                        }
                    }

                    // 2. Hardware Specs Sync (once per 24 hours or startup)
                    if ((DateTime.UtcNow - _lastHardwareSync).TotalHours >= 24)
                    {
                        _lastHardwareSync = DateTime.UtcNow;
                        try
                        {
                            var hwSpecs = _hardwareManager.CollectHardwareSpecs();
                            var hwRes = await _apiClient.SyncHardwareAsync(hwSpecs);
                            if (hwRes != null && hwRes.Success)
                            {
                                _logger.LogInformation("Spesifikasi hardware & {Count} partisi storage berhasil disinkronisasi ke server.", hwSpecs.Partitions.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Gagal mengumpulkan spesifikasi hardware: {Msg}", ex.Message);
                        }
                    }

                    // 3. Software Inventory Sync (once per 6 hours or startup)
                    if ((DateTime.UtcNow - _lastSoftwareSync).TotalHours >= 6)
                    {
                        _lastSoftwareSync = DateTime.UtcNow;
                        try
                        {
                            var installed = _softwareManager.GetInstalledSoftware();
                            var swRes = await _apiClient.SyncSoftwareAsync(new SoftwareSyncRequest { Software = installed });
                            if (swRes != null && swRes.Success)
                            {
                                _logger.LogInformation("Inventarisasi software berhasil: {Count} aplikasi terdaftar di server.", installed.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Gagal sinkronisasi inventarisasi software: {Msg}", ex.Message);
                        }
                    }

                    // 4. Blocklist Process Watcher & Screenshot Capture
                    try
                    {
                        int violations = await _blocklistWatcher.CheckAndEnforceAsync(
                            Environment.UserName,
                            async (procName, user, screenshotBytes) =>
                            {
                                _logger.LogWarning("⚠️ PELANGGARAN TERDETEKSI: Proses terlarang '{Proc}' dibuka oleh user '{User}'. Memaksa tutup dan mengambil screenshot...", procName, user);

                                try
                                {
                                    var repRes = await _apiClient.ReportViolationAsync(procName, user, screenshotBytes);
                                    if (repRes != null && repRes.Success)
                                    {
                                        _logger.LogInformation("Laporan pelanggaran '{Proc}' dan screenshot berhasil diunggah ke dashboard.", procName);
                                    }
                                    else
                                    {
                                        _offlineQueueManager.EnqueueViolation(procName, user, screenshotBytes);
                                        _logger.LogWarning("Server tidak merespons. Pelanggaran '{Proc}' disimpan ke antrian offline.", procName);
                                    }
                                }
                                catch
                                {
                                    _offlineQueueManager.EnqueueViolation(procName, user, screenshotBytes);
                                    _logger.LogWarning("Koneksi gagal. Pelanggaran '{Proc}' disimpan ke antrian offline.", procName);
                                }
                            },
                            isSimulationMode: config.IsSimulationMode
                        );

                        if (violations > 0)
                        {
                            _logger.LogInformation("Pemeriksaan proses: {Count} pelanggaran ditangani.", violations);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Kesalahan saat evaluasi blocklist watcher: {Msg}", ex.Message);
                    }

                    // 5. Check for Auto-Update every 6 hours
                    if ((DateTime.UtcNow - _lastUpdateCheck).TotalHours >= 6)
                    {
                        _lastUpdateCheck = DateTime.UtcNow;
                        var updateRes = await _updateManager.CheckForUpdateAsync("1.0.0");
                        if (updateRes.IsUpdateAvailable)
                        {
                            _logger.LogInformation("[AUTO-UPDATE] Versi baru {Ver} tersedia di GitHub Releases! Download: {Url}", updateRes.LatestVersion, updateRes.DownloadUrl ?? "-");
                        }
                    }

                    // 6. Check Local Schedules
                    var dueSchedule = _scheduleManager.CheckDueSchedule(DateTime.Now);
                    if (dueSchedule != null)
                    {
                        _logger.LogInformation("[JADWAL OTOMATIS] Menjalankan jadwal lokal #{Id} ({Tipe}) pada jam {Waktu}!", dueSchedule.Id, dueSchedule.Tipe, dueSchedule.Waktu);

                        var scheduleCmd = new PendingCommand
                        {
                            Id = dueSchedule.Id,
                            Type = dueSchedule.Tipe,
                            Payload = new CommandPayload
                            {
                                GraceSeconds = 60,
                                Message = $"Penjadwalan otomatis lab ({dueSchedule.Tipe}) pada jam {dueSchedule.Waktu}."
                            }
                        };

                        await _commandExecutor.ExecuteAsync(scheduleCmd, config, _kioskManager);
                    }

                    // 7. Send Heartbeat
                    var hbResponse = await _apiClient.SendHeartbeatAsync(
                        status: "online",
                        activeUser: Environment.UserName,
                        uptimeSeconds: (long)(Environment.TickCount64 / 1000)
                    );

                    if (hbResponse != null && hbResponse.Success && hbResponse.Data != null)
                    {
                        var data = hbResponse.Data;
                        _logger.LogInformation("Heartbeat berhasil terkirim. Komputer: {Name} (ID: {Id})", data.NamaPc, data.ComputerId);

                        // Flush any offline queued violations
                        try
                        {
                            int flushed = await _offlineQueueManager.FlushQueueAsync(_apiClient);
                            if (flushed > 0)
                            {
                                _logger.LogInformation("Antrian offline: {Count} laporan pelanggaran tertunda berhasil dikirim.", flushed);
                            }
                        }
                        catch { }

                        // 8. Process Pending Commands
                        if (data.PendingCommands.Count > 0)
                        {
                            _logger.LogInformation("Menerima {Count} perintah baru dari dashboard.", data.PendingCommands.Count);

                            foreach (var cmd in data.PendingCommands)
                            {
                                var result = await _commandExecutor.ExecuteAsync(cmd, config, _kioskManager);

                                string ackStatus = result.Success ? "executed" : "failed";
                                await _apiClient.AcknowledgeCommandAsync(cmd.Id, ackStatus, result.Message);

                                _logger.LogInformation("ACK perintah #{Id} terkirim: {Status} - {Msg}", cmd.Id, ackStatus, result.Message);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Heartbeat gagal atau tidak direspons server. Pesan: {Msg}", hbResponse?.Message ?? "Koneksi terputus");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kesalahan saat siklus heartbeat atau eksekusi perintah.");
                }

                int interval = config.HeartbeatIntervalSeconds > 0 ? config.HeartbeatIntervalSeconds : 15;
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }

            _logger.LogInformation("LabControl Agent Service dihentikan.");
        }
    }
}
