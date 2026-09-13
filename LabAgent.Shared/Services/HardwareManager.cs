using System.IO;
using System.Management;
using LabAgent.Shared.Models;

namespace LabAgent.Shared.Services
{
    public class HardwareManager
    {
        public HardwareSyncRequest CollectHardwareSpecs()
        {
            var request = new HardwareSyncRequest
            {
                OsVersion = Environment.OSVersion.ToString(),
            };

            // 1. Processor & Serial via WMI
            try
            {
                using var searcherCpu = new ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject obj in searcherCpu.Get())
                {
                    request.Processor = obj["Name"]?.ToString()?.Trim();
                    break;
                }
            }
            catch { }

            try
            {
                using var searcherBios = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
                foreach (ManagementObject obj in searcherBios.Get())
                {
                    string? serial = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serial) && !serial.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase))
                    {
                        request.SerialNumber = serial;
                        break;
                    }
                }
            }
            catch { }

            // 2. RAM capacity via Win32_PhysicalMemory or GC/System
            try
            {
                using var searcherRam = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                ulong totalBytes = 0;
                foreach (ManagementObject obj in searcherRam.Get())
                {
                    if (ulong.TryParse(obj["Capacity"]?.ToString(), out ulong cap))
                    {
                        totalBytes += cap;
                    }
                }

                if (totalBytes > 0)
                {
                    request.RamGb = Math.Round((double)totalBytes / (1024 * 1024 * 1024), 2);
                }
            }
            catch { }

            if (request.RamGb == null || request.RamGb <= 0)
            {
                // Fallback RAM estimation
                var mem = GC.GetGCMemoryInfo();
                if (mem.TotalAvailableMemoryBytes > 0)
                {
                    request.RamGb = Math.Round((double)mem.TotalAvailableMemoryBytes / (1024 * 1024 * 1024), 2);
                }
            }

            // 3. Disk Partitions via DriveInfo
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                    {
                        double totalGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 2);
                        double freeGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 2);

                        request.Partitions.Add(new DiskPartitionItem
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            TotalGb = totalGb,
                            FreeGb = freeGb
                        });
                    }
                }
            }
            catch { }

            return request;
        }
    }
}
