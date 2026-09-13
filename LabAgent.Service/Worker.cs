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
        private DateTime _lastConfigSync = DateTime.MinValue;
        private DateTime _lastUpdateCheck = DateTime.MinValue;

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

            var cfg = _configManager.Load();
            _updateManager = new UpdateManager("MuslimGunawan/smartlab-agent");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=================================================");
            _logger.LogInformation("SmartLab Unimal Agent Service Dimulai");
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
                    // 1. Sync Config & Schedules every 5 minutes or at startup
                    if ((DateTime.UtcNow - _lastConfigSync).TotalMinutes >= 5)
                    {
                        var configRes = await _apiClient.GetConfigAsync();
                        if (configRes != null && configRes.Success && configRes.Data != null)
                        {
                            _lastConfigSync = DateTime.UtcNow;
                            _scheduleManager.UpdateSchedules(configRes.Data.Schedules);
                            _logger.LogInformation("Sinkronisasi konfigurasi berhasil: {Count} jadwal aktif termuat.", configRes.Data.Schedules.Count);

                            // Apply Kiosk settings if defined
                            if (configRes.Data.KioskSettings != null)
                            {
                                _kioskManager.ApplyPolicy(configRes.Data.KioskSettings);
                            }
                        }
                    }

                    // 2. Check for Auto-Update every 6 hours or at startup
                    if ((DateTime.UtcNow - _lastUpdateCheck).TotalHours >= 6)
                    {
                        _lastUpdateCheck = DateTime.UtcNow;
                        var updateRes = await _updateManager.CheckForUpdateAsync("1.0.0");
                        if (updateRes.IsUpdateAvailable)
                        {
                            _logger.LogInformation("[AUTO-UPDATE] Versi baru {Ver} tersedia di GitHub Releases! Download: {Url}", updateRes.LatestVersion, updateRes.DownloadUrl ?? "-");
                        }
                    }

                    // 3. Check Local Schedules
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

                    // 4. Send Heartbeat
                    var hbResponse = await _apiClient.SendHeartbeatAsync(
                        status: "online",
                        activeUser: Environment.UserName,
                        uptimeSeconds: (long)(Environment.TickCount64 / 1000)
                    );

                    if (hbResponse != null && hbResponse.Success && hbResponse.Data != null)
                    {
                        var data = hbResponse.Data;
                        _logger.LogInformation("Heartbeat berhasil terkirim. Komputer: {Name} (ID: {Id})", data.NamaPc, data.ComputerId);

                        // 5. Process Pending Commands
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
