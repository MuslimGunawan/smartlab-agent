using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using LabAgent.Shared.Models;

namespace LabAgent.Shared.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly AgentConfigManager _configManager;
        private AgentLocalConfig _config;

        public ApiClient(AgentConfigManager configManager, HttpClient? httpClient = null)
        {
            _configManager = configManager;
            _config = _configManager.Load();
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public AgentLocalConfig CurrentConfig => _config;

        public void ReloadConfig()
        {
            _config = _configManager.Load();
        }

        public async Task<ApiResponse<RegisterResponseData>?> RegisterWithPairingCodeAsync(string pairingCode, string? customName = null)
        {
            string hostname = Environment.MachineName;
            string? mac = GetMacAddress();

            var requestBody = new RegisterRequest
            {
                PairingCode = pairingCode.Trim(),
                Hostname = hostname,
                MacAddress = mac,
                NamaPc = customName ?? $"PC-{hostname}"
            };

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/register";
            string json = JsonSerializer.Serialize(requestBody);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            string responseText = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<ApiResponse<RegisterResponseData>>(responseText);

            if (response.IsSuccessStatusCode && result != null && result.Success && result.Data != null)
            {
                // Save paired state
                _config.DeviceToken = result.Data.DeviceToken;
                _config.ComputerId = result.Data.ComputerId;
                _config.ComputerName = result.Data.NamaPc;
                _config.LabId = result.Data.LabId;
                _config.LabName = result.Data.LabNama;
                _configManager.Save(_config);
            }

            return result;
        }

        public async Task<ApiResponse<HeartbeatResponseData>?> SendHeartbeatAsync(
            string status = "online",
            string? activeUser = null,
            long uptimeSeconds = 0)
        {
            if (string.IsNullOrEmpty(_config.DeviceToken))
            {
                return null;
            }

            string ip = GetLocalIpAddress() ?? "127.0.0.1";
            string user = activeUser ?? Environment.UserName;

            var requestBody = new HeartbeatRequest
            {
                Status = status,
                Ip = ip,
                ActiveUser = user,
                UptimeSeconds = uptimeSeconds > 0 ? uptimeSeconds : (long)(Environment.TickCount64 / 1000),
                AgentVersion = "1.0.0",
                ConfigVersion = 1
            };

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/heartbeat";
            string json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiResponse<HeartbeatResponseData>>(responseText);
        }

        public async Task<ApiResponse<object>?> AcknowledgeCommandAsync(int commandId, string status, string? message = null)
        {
            if (string.IsNullOrEmpty(_config.DeviceToken))
            {
                return null;
            }

            var requestBody = new CommandAckRequest
            {
                Status = status,
                ResultMessage = message
            };

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/commands/{commandId}/ack";
            string json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiResponse<object>>(responseText);
        }

        public async Task<ApiResponse<AgentConfigResponseData>?> GetConfigAsync()
        {
            if (string.IsNullOrEmpty(_config.DeviceToken))
            {
                return null;
            }

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/config";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string responseText = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse<AgentConfigResponseData>>(responseText);
        }

        public async Task<ApiResponse<object>?> SyncHardwareAsync(HardwareSyncRequest hardwareReq)
        {
            if (string.IsNullOrEmpty(_config.DeviceToken))
            {
                return null;
            }

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/hardware";
            string json = JsonSerializer.Serialize(hardwareReq);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiResponse<object>>(responseText);
        }

        public async Task<ApiResponse<object>?> SyncSoftwareAsync(SoftwareSyncRequest softwareReq)
        {
            if (string.IsNullOrEmpty(_config.DeviceToken))
            {
                return null;
            }

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/software";
            string json = JsonSerializer.Serialize(softwareReq);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiResponse<object>>(responseText);
        }

        public async Task<ApiResponse<object>?> ReportViolationAsync(string processName, string? activeUser, byte[]? screenshotBytes)
        {
            if (string.IsNullOrEmpty(_config.DeviceToken))
            {
                return null;
            }

            string url = $"{_config.ServerBaseUrl.TrimEnd('/')}/agent/violations";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.DeviceToken);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(processName), "process_name");
            if (!string.IsNullOrEmpty(activeUser))
            {
                content.Add(new StringContent(activeUser), "active_user");
            }
            content.Add(new StringContent(DateTime.UtcNow.ToString("o")), "detected_at");

            if (screenshotBytes != null && screenshotBytes.Length > 0)
            {
                var imageContent = new ByteArrayContent(screenshotBytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "screenshot", "violation.jpg");
                content.Add(new StringContent(Convert.ToBase64String(screenshotBytes)), "screenshot_base64");
            }

            request.Content = content;
            var response = await _httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ApiResponse<object>>(responseText);
        }

        private string? GetMacAddress()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        var bytes = nic.GetPhysicalAddress().GetAddressBytes();
                        if (bytes.Length > 0)
                        {
                            return string.Join(":", bytes.Select(b => b.ToString("X2")));
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private string? GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
