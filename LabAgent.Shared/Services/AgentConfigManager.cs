using System.Text.Json;
using LabAgent.Shared.Models;

namespace LabAgent.Shared.Services
{
    public class AgentConfigManager
    {
        private readonly string _configFilePath;

        public AgentConfigManager(string? customPath = null)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                _configFilePath = customPath;
            }
            else
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dir = Path.Combine(programData, "LabControl");
                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch
                    {
                        // Fallback to local application base directory if ProgramData is restricted
                        dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                        Directory.CreateDirectory(dir);
                    }
                }
                _configFilePath = Path.Combine(dir, "agent_config.json");
            }
        }

        public AgentLocalConfig Load()
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultConfig = new AgentLocalConfig();
                Save(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(_configFilePath);
                return JsonSerializer.Deserialize<AgentLocalConfig>(json) ?? new AgentLocalConfig();
            }
            catch
            {
                return new AgentLocalConfig();
            }
        }

        public void Save(AgentLocalConfig config)
        {
            try
            {
                string dir = Path.GetDirectoryName(_configFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Gagal menyimpan konfigurasi: {ex.Message}");
            }
        }

        public bool IsPaired(AgentLocalConfig config)
        {
            return !string.IsNullOrWhiteSpace(config.DeviceToken) && config.ComputerId.HasValue;
        }
    }
}
