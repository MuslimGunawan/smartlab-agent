using System.IO;
using System.Text.Json;
using LabAgent.Shared.Models;

namespace LabAgent.Shared.Services
{
    public class QueuedViolationItem
    {
        public string ProcessName { get; set; } = string.Empty;
        public string? ActiveUser { get; set; }
        public string? ScreenshotBase64 { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class OfflineQueueData
    {
        public List<QueuedViolationItem> Violations { get; set; } = new();
    }

    public class OfflineQueueManager
    {
        private readonly string _queueFilePath;
        private readonly object _lock = new();

        public OfflineQueueManager(string? baseDir = null)
        {
            string dir = baseDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LabControl"
            );

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _queueFilePath = Path.Combine(dir, "offline_queue.json");
        }

        public void EnqueueViolation(string processName, string? activeUser, byte[]? screenshotBytes)
        {
            lock (_lock)
            {
                var queue = LoadQueueInternal();
                queue.Violations.Add(new QueuedViolationItem
                {
                    ProcessName = processName,
                    ActiveUser = activeUser,
                    ScreenshotBase64 = screenshotBytes != null ? Convert.ToBase64String(screenshotBytes) : null,
                    Timestamp = DateTime.UtcNow
                });
                SaveQueueInternal(queue);
            }
        }

        public async Task<int> FlushQueueAsync(ApiClient apiClient)
        {
            List<QueuedViolationItem> violationsToFlush;

            lock (_lock)
            {
                var queue = LoadQueueInternal();
                if (queue.Violations.Count == 0) return 0;
                violationsToFlush = queue.Violations.ToList();
            }

            var remaining = new List<QueuedViolationItem>();
            int flushedCount = 0;

            foreach (var item in violationsToFlush)
            {
                try
                {
                    byte[]? imageBytes = !string.IsNullOrEmpty(item.ScreenshotBase64)
                        ? Convert.FromBase64String(item.ScreenshotBase64)
                        : null;

                    var res = await apiClient.ReportViolationAsync(item.ProcessName, item.ActiveUser, imageBytes);
                    if (res != null && res.Success)
                    {
                        flushedCount++;
                    }
                    else
                    {
                        remaining.Add(item);
                    }
                }
                catch
                {
                    remaining.Add(item);
                }
            }

            lock (_lock)
            {
                var currentQueue = LoadQueueInternal();
                currentQueue.Violations = remaining;
                SaveQueueInternal(currentQueue);
            }

            return flushedCount;
        }

        private OfflineQueueData LoadQueueInternal()
        {
            try
            {
                if (File.Exists(_queueFilePath))
                {
                    string json = File.ReadAllText(_queueFilePath);
                    return JsonSerializer.Deserialize<OfflineQueueData>(json) ?? new OfflineQueueData();
                }
            }
            catch { }
            return new OfflineQueueData();
        }

        private void SaveQueueInternal(OfflineQueueData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_queueFilePath, json);
            }
            catch { }
        }
    }
}
