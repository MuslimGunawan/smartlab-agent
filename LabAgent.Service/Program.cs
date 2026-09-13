using LabAgent.Service;
using LabAgent.Shared.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add Windows Service lifecycle support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "LabControlUnimalAgent";
});

// Register Shared Services
builder.Services.AddSingleton<AgentConfigManager>();
builder.Services.AddSingleton<ApiClient>();
builder.Services.AddSingleton<CommandExecutor>();

// Register Background Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// CLI Utility Support: Quick Pairing from command line (e.g. `dotnet run -- --pair UNM-XXXXXX`)
var configManager = host.Services.GetRequiredService<AgentConfigManager>();
var apiClient = host.Services.GetRequiredService<ApiClient>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

bool shouldExit = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--pair" && i + 1 < args.Length)
    {
        string pairCode = args[i + 1];
        logger.LogInformation("Mencoba melakukan pairing dengan kode: {Code}...", pairCode);
        try
        {
            var res = apiClient.RegisterWithPairingCodeAsync(pairCode).GetAwaiter().GetResult();
            if (res != null && res.Success)
            {
                logger.LogInformation("Pairing BERHASIL! Terhubung sebagai '{Name}' ke {Lab}", res.Data?.NamaPc, res.Data?.LabNama);
            }
            else
            {
                logger.LogError("Pairing GAGAL: {Msg}", res?.Message ?? "Respon tidak valid");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terjadi kesalahan saat pairing");
        }
    }
    else if (args[i] == "--sim")
    {
        var cfg = configManager.Load();
        cfg.IsSimulationMode = true;
        configManager.Save(cfg);
        logger.LogInformation("Mode Simulasi diaktifkan pada konfigurasi lokal.");
    }
    else if (args[i] == "--url" && i + 1 < args.Length)
    {
        var cfg = configManager.Load();
        cfg.ServerBaseUrl = args[i + 1];
        configManager.Save(cfg);
        logger.LogInformation("Server URL diperbarui ke: {Url}", cfg.ServerBaseUrl);
    }
    else if (args[i] == "--exit")
    {
        shouldExit = true;
    }
}

if (!shouldExit)
{
    host.Run();
}
else
{
    logger.LogInformation("Operasi CLI selesai, keluar.");
}
