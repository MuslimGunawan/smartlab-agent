using System.Security.Principal;
using Microsoft.Win32;

namespace LabAgent.Shared.Services
{
    public class KioskPolicy
    {
        public bool DisableTaskmgr { get; set; }
        public bool DisableCmd { get; set; }
        public bool DisableRegedit { get; set; }
        public bool DisableControlPanel { get; set; }
    }

    public class KioskManager
    {
        private const string SystemPoliciesKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string ExplorerPoliciesKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        private const string CmdPoliciesKey = @"Software\Policies\Microsoft\Windows\System";

        public bool IsCurrentUserAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public bool ApplyPolicy(KioskPolicy policy, bool skipAdminCheck = false)
        {
            if (!skipAdminCheck && IsCurrentUserAdmin())
            {
                // Safety: Do not restrict ASLAB / Administrators
                return true;
            }

            bool appliedAny = false;

            // Try CurrentUser first, then fallback to LocalMachine if running as SYSTEM/Admin
            RegistryKey[] rootKeys = new[] { Registry.CurrentUser, Registry.LocalMachine };

            foreach (var root in rootKeys)
            {
                try
                {
                    // 1. Task Manager & Regedit
                    using (var key = root.CreateSubKey(SystemPoliciesKey, writable: true))
                    {
                        if (key != null)
                        {
                            if (policy.DisableTaskmgr)
                                key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                            else
                                key.DeleteValue("DisableTaskMgr", throwOnMissingValue: false);

                            if (policy.DisableRegedit)
                                key.SetValue("DisableRegistryTools", 1, RegistryValueKind.DWord);
                            else
                                key.DeleteValue("DisableRegistryTools", throwOnMissingValue: false);
                        }
                    }

                    // 2. Command Prompt
                    using (var key = root.CreateSubKey(CmdPoliciesKey, writable: true))
                    {
                        if (key != null)
                        {
                            if (policy.DisableCmd)
                                key.SetValue("DisableCMD", 1, RegistryValueKind.DWord);
                            else
                                key.DeleteValue("DisableCMD", throwOnMissingValue: false);
                        }
                    }

                    // 3. Control Panel
                    using (var key = root.CreateSubKey(ExplorerPoliciesKey, writable: true))
                    {
                        if (key != null)
                        {
                            if (policy.DisableControlPanel)
                                key.SetValue("NoControlPanel", 1, RegistryValueKind.DWord);
                            else
                                key.DeleteValue("NoControlPanel", throwOnMissingValue: false);
                        }
                    }

                    appliedAny = true;
                    break; // Applied successfully
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[KioskManager] Info: Root {root.Name} not accessible: {ex.Message}");
                }
            }

            return appliedAny;
        }

        public bool ClearAllRestrictions()
        {
            return ApplyPolicy(new KioskPolicy
            {
                DisableTaskmgr = false,
                DisableCmd = false,
                DisableRegedit = false,
                DisableControlPanel = false
            }, skipAdminCheck: true);
        }

        public KioskPolicy GetCurrentPolicy()
        {
            var policy = new KioskPolicy();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SystemPoliciesKey))
                {
                    if (key != null)
                    {
                        policy.DisableTaskmgr = Convert.ToInt32(key.GetValue("DisableTaskMgr", 0)) == 1;
                        policy.DisableRegedit = Convert.ToInt32(key.GetValue("DisableRegistryTools", 0)) == 1;
                    }
                }

                using (var key = Registry.CurrentUser.OpenSubKey(CmdPoliciesKey))
                {
                    if (key != null)
                    {
                        policy.DisableCmd = Convert.ToInt32(key.GetValue("DisableCMD", 0)) == 1;
                    }
                }

                using (var key = Registry.CurrentUser.OpenSubKey(ExplorerPoliciesKey))
                {
                    if (key != null)
                    {
                        policy.DisableControlPanel = Convert.ToInt32(key.GetValue("NoControlPanel", 0)) == 1;
                    }
                }
            }
            catch { }

            return policy;
        }
    }
}
