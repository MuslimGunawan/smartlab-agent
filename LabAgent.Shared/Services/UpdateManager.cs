using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabAgent.Shared.Services
{
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("published_at")]
        public string PublishedAt { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "1.0.0";
        public string LatestVersion { get; set; } = string.Empty;
        public string? ReleaseNotes { get; set; }
        public string? DownloadUrl { get; set; }
    }

    public class UpdateManager
    {
        private readonly HttpClient _httpClient;
        private readonly string _githubRepo;

        public UpdateManager(string? githubRepo = null)
        {
            _githubRepo = !string.IsNullOrWhiteSpace(githubRepo) ? githubRepo : "MuslimGunawan/smartlab-agent";
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SmartLab-Agent", "1.0"));
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(string currentVersion = "1.0.0")
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = currentVersion
            };

            try
            {
                string url = $"https://api.github.com/repos/{_githubRepo}/releases/latest";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                {
                    return result;
                }

                string latestClean = release.TagName.TrimStart('v', 'V').Trim();
                result.LatestVersion = latestClean;
                result.ReleaseNotes = release.Body;

                var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                result.DownloadUrl = asset?.DownloadUrl;

                if (Version.TryParse(currentVersion, out var curVer) && Version.TryParse(latestClean, out var latVer))
                {
                    result.IsUpdateAvailable = latVer > curVer;
                }
                else
                {
                    result.IsUpdateAvailable = string.Compare(latestClean, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UpdateManager] Error checking updates from {_githubRepo}: {ex.Message}");
                return result;
            }
        }
    }
}
