using System.Drawing.Drawing2D;

namespace LabAgent.Tray.Forms
{
    public class BroadcastNotificationForm : Form
    {
        private readonly System.Windows.Forms.Timer _countdownTimer;
        private int _secondsRemaining;
        private readonly int _totalSeconds;
        private readonly Label _titleLabel;
        private readonly Label _messageLabel;
        private readonly Label _timerLabel;
        private readonly Panel _progressBarPanel;

        public BroadcastNotificationForm(string message, int durationSeconds = 15, string title = "PENGUMUMAN ASLAB TI UNIMAL")
        {
            _secondsRemaining = Math.Max(5, durationSeconds);
            _totalSeconds = _secondsRemaining;

            // Form properties
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            Width = 480;
            Height = 150;
            BackColor = Color.FromArgb(15, 23, 42); // Dark slate background

            // Position at top center of primary screen
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            Location = new Point((screen.Width - Width) / 2, screen.Top + 24);

            // Title
            _titleLabel = new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(226, 163, 19), // #E2A313 Unimal Gold
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(20, 16),
                AutoSize = true
            };

            // Close button (x)
            var closeButton = new Label
            {
                Text = "×",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(Width - 36, 8),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            closeButton.Click += (s, e) => Close();

            // Message
            _messageLabel = new Label
            {
                Text = message,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                Location = new Point(20, 44),
                Width = Width - 40,
                Height = 60
            };

            // Timer display
            _timerLabel = new Label
            {
                Text = $"Menutup otomatis dalam {_secondsRemaining}s",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                Location = new Point(20, 114),
                AutoSize = true
            };

            // Progress bar
            _progressBarPanel = new Panel
            {
                BackColor = Color.FromArgb(0, 147, 68), // #009344 Unimal Green
                Height = 4,
                Width = Width,
                Location = new Point(0, Height - 4)
            };

            Controls.Add(_titleLabel);
            Controls.Add(closeButton);
            Controls.Add(_messageLabel);
            Controls.Add(_timerLabel);
            Controls.Add(_progressBarPanel);

            // Timer setup
            _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _countdownTimer.Tick += (s, e) =>
            {
                _secondsRemaining--;
                _timerLabel.Text = $"Menutup otomatis dalam {_secondsRemaining}s";
                int barWidth = (int)((float)_secondsRemaining / _totalSeconds * Width);
                _progressBarPanel.Width = Math.Max(0, barWidth);

                if (_secondsRemaining <= 0)
                {
                    _countdownTimer.Stop();
                    Close();
                }
            };
            _countdownTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw subtle green border
            using var pen = new Pen(Color.FromArgb(0, 147, 68), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _countdownTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
