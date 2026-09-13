using System.IO;
using System.Runtime.InteropServices;

namespace LabAgent.Shared.Services
{
    public class CleanupResult
    {
        public bool Success { get; set; }
        public long BytesFreed { get; set; }
        public int FilesDeleted { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CleanupManager
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public CleanupResult ExecuteCleanup()
        {
            var result = new CleanupResult { Success = true };

            // 1. User %TEMP%
            try
            {
                string tempPath = Path.GetTempPath();
                CleanDirectory(tempPath, result);
            }
            catch { }

            // 2. Windows Temp
            try
            {
                string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                if (Directory.Exists(winTemp))
                {
                    CleanDirectory(winTemp, result);
                }
            }
            catch { }

            // 3. Recycle Bin
            try
            {
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            }
            catch { }

            double mbFreed = Math.Round((double)result.BytesFreed / (1024 * 1024), 2);
            if (mbFreed > 1024)
            {
                double gbFreed = Math.Round(mbFreed / 1024, 2);
                result.Message = $"Pembersihan selesai: Berhasil membebaskan {gbFreed} GB ruang disk ({result.FilesDeleted} file dihapus) dan mengosongkan Recycle Bin.";
            }
            else
            {
                result.Message = $"Pembersihan selesai: Berhasil membebaskan {mbFreed} MB ruang disk ({result.FilesDeleted} file dihapus) dan mengosongkan Recycle Bin.";
            }

            return result;
        }

        private void CleanDirectory(string dirPath, CleanupResult result)
        {
            if (!Directory.Exists(dirPath)) return;

            var dir = new DirectoryInfo(dirPath);

            foreach (var file in dir.EnumerateFiles())
            {
                try
                {
                    long length = file.Length;
                    file.Delete();
                    result.BytesFreed += length;
                    result.FilesDeleted++;
                }
                catch
                {
                    // Locked file in use by active processes, safely skip
                }
            }

            foreach (var subDir in dir.EnumerateDirectories())
            {
                try
                {
                    subDir.Delete(recursive: true);
                }
                catch
                {
                    // Subdirectory contains files in use, safely skip
                }
            }
        }
    }
}
