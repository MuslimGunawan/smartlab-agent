using Microsoft.Win32;
using LabAgent.Shared.Models;

namespace LabAgent.Shared.Services
{
    public class SoftwareManager
    {
        private static readonly string[] RegistryUninstallKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public List<SoftwareItem> GetInstalledSoftware()
        {
            var softwareList = new Dictionary<string, SoftwareItem>(StringComparer.OrdinalIgnoreCase);

            // Scan HKLM (64-bit and 32-bit WOW64)
            foreach (var subKeyPath in RegistryUninstallKeys)
            {
                ScanRegistryKey(Registry.LocalMachine, subKeyPath, softwareList);
            }

            // Scan HKCU (User-scoped installs: Discord, VS Code User, Chrome)
            ScanRegistryKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", softwareList);

            return softwareList.Values
                .OrderBy(s => s.Nama)
                .ToList();
        }

        private void ScanRegistryKey(RegistryKey root, string subKeyPath, Dictionary<string, SoftwareItem> softwareMap)
        {
            try
            {
                using var baseKey = root.OpenSubKey(subKeyPath);
                if (baseKey == null) return;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var key = baseKey.OpenSubKey(subKeyName);
                        if (key == null) continue;

                        // Check SystemComponent
                        int isSystem = Convert.ToInt32(key.GetValue("SystemComponent", 0));
                        if (isSystem == 1) continue;

                        // Check ParentKeyName (sub-component)
                        string? parent = key.GetValue("ParentKeyName")?.ToString();
                        if (!string.IsNullOrEmpty(parent)) continue;

                        string? name = key.GetValue("DisplayName")?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // Ignore Windows Update patches
                        if (name.StartsWith("KB", StringComparison.OrdinalIgnoreCase) && name.Length > 2 && char.IsDigit(name[2]))
                        {
                            continue;
                        }

                        string? version = key.GetValue("DisplayVersion")?.ToString()?.Trim();
                        string? date = key.GetValue("InstallDate")?.ToString()?.Trim();
                        string? uninstallStr = key.GetValue("QuietUninstallString")?.ToString()
                                              ?? key.GetValue("UninstallString")?.ToString();

                        long? sizeKb = null;
                        object? sizeVal = key.GetValue("EstimatedSize");
                        if (sizeVal != null && long.TryParse(sizeVal.ToString(), out long sz))
                        {
                            sizeKb = sz;
                        }

                        if (!softwareMap.ContainsKey(name))
                        {
                            softwareMap[name] = new SoftwareItem
                            {
                                Nama = name,
                                Versi = version,
                                TanggalInstall = date,
                                UkuranKb = sizeKb,
                                UninstallString = uninstallStr
                            };
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
