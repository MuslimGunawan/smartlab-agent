using System.Drawing;
using System.Drawing.Drawing2D;
using LabAgent.Shared.Services;

namespace LabAgent.Tray
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly AgentConfigManager _configManager;
        private readonly ApiClient _apiClient;
        private readonly System.Windows.Forms.Timer _statusTimer;

        public TrayApplicationContext()
        {
            _configManager = new AgentConfigManager();
            _apiClient = new ApiClient(_configManager);

            // Programmatically draw the Unimal Monitor icon (16x16 with status dot)
            var icon = CreateAppIcon();

            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "LabControl Unimal — PC Dikelola Lab TI",
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };

            _notifyIcon.DoubleClick += (s, e) => ShowInfo();

            // Status refresh timer
            _statusTimer = new System.Windows.Forms.Timer
            {
                Interval = 10000 // 10 seconds
            };
            _statusTimer.Tick += (s, e) => UpdateStatus();
            _statusTimer.Start();

            // Initial balloon tip
            var cfg = _configManager.Load();
            if (_configManager.IsPaired(cfg))
            {
                _notifyIcon.ShowBalloonTip(
                    3000,
                    "LabControl Unimal Aktif",
                    $"PC terhubung ke {cfg.LabName ?? "Lab"} ({cfg.ComputerName})",
                    ToolTipIcon.Info
                );
            }
            else
            {
                _notifyIcon.ShowBalloonTip(
                    4000,
                    "LabControl Perlu Dipasangkan",
                    "Klik kanan ikon ini dan pilih 'Pasangkan ke Ruangan Lab'.",
                    ToolTipIcon.Warning
                );
            }
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            var cfg = _configManager.Load();
            bool isPaired = _configManager.IsPaired(cfg);

            var titleItem = new ToolStripMenuItem("LabControl Unimal (v1.0.0)")
            {
                Enabled = false,
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);

            string statusText = isPaired ? $"Terhubung: {cfg.ComputerName} ({cfg.LabName})" : "Status: Belum Dipasangkan";
            var statusItem = new ToolStripMenuItem(statusText)
            {
                Enabled = false
            };
            menu.Items.Add(statusItem);

            menu.Items.Add(new ToolStripSeparator());

            var pairItem = new ToolStripMenuItem("Pasangkan PC ke Ruangan Lab...", null, (s, e) => PromptPairing());
            menu.Items.Add(pairItem);

            var simItem = new ToolStripMenuItem($"Mode Simulasi: {(cfg.IsSimulationMode ? "AKTIF" : "NONAKTIF")}", null, (s, e) => ToggleSimulation());
            menu.Items.Add(simItem);

            var infoItem = new ToolStripMenuItem("Informasi & Status PC", null, (s, e) => ShowInfo());
            menu.Items.Add(infoItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Tutup Tray UI", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                Application.Exit();
            });
            menu.Items.Add(exitItem);

            return menu;
        }

        private void UpdateStatus()
        {
            _notifyIcon.ContextMenuStrip = BuildContextMenu();
        }

        private async void PromptPairing()
        {
            string code = PromptDialog.Show("Masukkan Kode Pairing yang didapat dari Dashboard ASLAB (contoh: UNM-A1B2C3):", "Pairing Komputer Lab");
            if (string.IsNullOrWhiteSpace(code)) return;

            try
            {
                _notifyIcon.ShowBalloonTip(2000, "Menghubungkan...", "Memvalidasi kode pairing ke server...", ToolTipIcon.Info);
                var res = await _apiClient.RegisterWithPairingCodeAsync(code.Trim());

                if (res != null && res.Success && res.Data != null)
                {
                    _notifyIcon.ShowBalloonTip(
                        4000,
                        "Pairing Berhasil!",
                        $"Komputer berhasil dipasangkan ke {res.Data.LabNama} sebagai '{res.Data.NamaPc}'.",
                        ToolTipIcon.Info
                    );
                    UpdateStatus();
                }
                else
                {
                    MessageBox.Show(res?.Message ?? "Gagal menghubungkan ke server.", "Pairing Gagal", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Terjadi kesalahan koneksi: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleSimulation()
        {
            var cfg = _configManager.Load();
            cfg.IsSimulationMode = !cfg.IsSimulationMode;
            _configManager.Save(cfg);

            string msg = cfg.IsSimulationMode
                ? "Mode Simulasi AKTIF. Perintah shutdown/restart tidak akan mematikan Windows (Aman untuk dev)."
                : "Mode Simulasi NONAKTIF. Perintah akan mematikan Windows sungguhan.";

            MessageBox.Show(msg, "Mode Simulasi Diubah", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateStatus();
        }

        private void ShowInfo()
        {
            var cfg = _configManager.Load();
            string info = $"Sistem Manajemen Lab Komputer TI Unimal\n\n" +
                          $"Nama PC: {cfg.ComputerName ?? "Belum terdaftar"}\n" +
                          $"Ruangan: {cfg.LabName ?? "Belum terdaftar"}\n" +
                          $"Komputer ID: {cfg.ComputerId?.ToString() ?? "-"}\n" +
                          $"Server URL: {cfg.ServerBaseUrl}\n" +
                          $"Simulation Mode: {(cfg.IsSimulationMode ? "Ya (Aman)" : "Tidak")}\n" +
                          $"Hostname: {Environment.MachineName}\n" +
                          $"User Windows: {Environment.UserName}\n\n" +
                          $"Aplikasi ini berjalan dengan persetujuan ASLAB TI Unimal untuk keperluan efisiensi dan keamanan operasional lab.";

            MessageBox.Show(info, "Tentang LabControl Agent", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static Icon CreateAppIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Unimal Green Screen Frame
                using var greenBrush = new SolidBrush(Color.FromArgb(0, 147, 68)); // #009344
                g.FillRoundedRectangle(greenBrush, new Rectangle(2, 2, 28, 20), 4);

                // Dark Inner Display
                using var screenBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                g.FillRoundedRectangle(screenBrush, new Rectangle(5, 5, 22, 14), 2);

                // Monitor Stand
                using var standBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                g.FillRectangle(standBrush, new Rectangle(14, 22, 4, 5));
                g.FillRoundedRectangle(standBrush, new Rectangle(10, 27, 12, 3), 1);

                // Unimal Gold Status Dot (#E2A313)
                using var goldBrush = new SolidBrush(Color.FromArgb(226, 163, 19));
                g.FillEllipse(goldBrush, new Rectangle(20, 18, 10, 10));
                using var whitePen = new Pen(Color.White, 1.5f);
                g.DrawEllipse(whitePen, new Rectangle(20, 18, 10, 10));
            }

            return Icon.FromHandle(bmp.GetHicon());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusTimer.Dispose();
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }

    public static class PromptDialog
    {
        public static string Show(string prompt, string title)
        {
            var form = new Form
            {
                Width = 420,
                Height = 190,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new Label { Left = 20, Top = 16, Width = 360, Text = prompt };
            var textBox = new TextBox { Left = 20, Top = 56, Width = 360, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            var okButton = new Button { Text = "Pasangkan", Left = 180, Width = 95, Top = 100, DialogResult = DialogResult.OK };
            var cancelButton = new Button { Text = "Batal", Left = 285, Width = 95, Top = 100, DialogResult = DialogResult.Cancel };

            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(okButton);
            form.Controls.Add(cancelButton);
            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;

            return form.ShowDialog() == DialogResult.OK ? textBox.Text : string.Empty;
        }
    }
}
