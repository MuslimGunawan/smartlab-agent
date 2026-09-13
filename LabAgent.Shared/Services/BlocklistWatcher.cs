using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace LabAgent.Shared.Services
{
    public class BlocklistWatcher
    {
        private readonly HashSet<string> _blocklist = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastViolationTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _violationCooldown = TimeSpan.FromSeconds(30);

        public void UpdateBlocklist(IEnumerable<string>? processes)
        {
            lock (_blocklist)
            {
                _blocklist.Clear();
                if (processes != null)
                {
                    foreach (var proc in processes)
                    {
                        string clean = proc.Trim().ToLowerInvariant();
                        if (clean.EndsWith(".exe"))
                        {
                            clean = clean.Substring(0, clean.Length - 4);
                        }
                        if (!string.IsNullOrEmpty(clean))
                        {
                            _blocklist.Add(clean);
                        }
                    }
                }
            }
        }

        public async Task<int> CheckAndEnforceAsync(
            string? activeUser,
            Func<string, string?, byte[]?, Task> onViolationReport,
            bool isSimulationMode = false)
        {
            List<string> currentBlocklist;
            lock (_blocklist)
            {
                currentBlocklist = _blocklist.ToList();
            }

            if (currentBlocklist.Count == 0) return 0;

            int violationsFound = 0;
            var running = Process.GetProcesses();

            foreach (var proc in running)
            {
                try
                {
                    string procName = proc.ProcessName.ToLowerInvariant();

                    if (currentBlocklist.Contains(procName))
                    {
                        // Check cooldown to avoid uploading 10 screenshots per second
                        if (_lastViolationTime.TryGetValue(procName, out var lastTime) &&
                            DateTime.UtcNow - lastTime < _violationCooldown)
                        {
                            continue;
                        }

                        _lastViolationTime[procName] = DateTime.UtcNow;
                        violationsFound++;

                        // 1. Capture screen before killing
                        byte[]? screenshotBytes = CapturePrimaryScreenJpeg();

                        // 2. Kill the forbidden process
                        if (!isSimulationMode)
                        {
                            try
                            {
                                proc.Kill(entireProcessTree: true);
                            }
                            catch { }
                        }

                        // 3. Report violation to dashboard
                        string formalProcName = $"{procName}.exe";
                        await onViolationReport(formalProcName, activeUser, screenshotBytes);
                    }
                }
                catch { }
            }

            return violationsFound;
        }

        public byte[]? CapturePrimaryScreenJpeg()
        {
            try
            {
                var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    bounds = new Rectangle(0, 0, 1920, 1080);
                }

                using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bitmap))
                {
                    try
                    {
                        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    }
                    catch (Exception gEx)
                    {
                        Console.WriteLine($"[BlocklistWatcher] CopyFromScreen info: {gEx.Message}. Menggunakan banner tangkapan bukti.");
                        g.Clear(Color.FromArgb(15, 23, 42));
                        using var greenBrush = new SolidBrush(Color.FromArgb(0, 147, 68));
                        g.FillRectangle(greenBrush, 0, 0, bounds.Width, 80);
                        using var font = new Font("Arial", 16, FontStyle.Bold);
                        using var textBrush = new SolidBrush(Color.White);
                        g.DrawString("SmartLab Unimal - Bukti Pelanggaran Aplikasi Terdeteksi", font, textBrush, 30, 25);
                        using var subFont = new Font("Arial", 12);
                        g.DrawString($"Waktu: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | User: {Environment.UserName} | Host: {Environment.MachineName}", subFont, textBrush, 30, 120);
                    }
                }

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BlocklistWatcher] Screenshot error: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }
    }
}
