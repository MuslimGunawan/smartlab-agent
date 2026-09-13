using System.Diagnostics;
using LabAgent.Shared.Models;
using LabAgent.Shared.Services;

namespace LabAgent.Service
{
    public class CommandExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CommandExecutor
    {
        private readonly ILogger<CommandExecutor> _logger;

        public CommandExecutor(ILogger<CommandExecutor> logger)
        {
            _logger = logger;
        }

        public async Task<CommandExecutionResult> ExecuteAsync(PendingCommand command, AgentLocalConfig config, KioskManager? kioskManager = null)
        {
            _logger.LogInformation("Mengeksekusi perintah #{Id} tipe: {Type}", command.Id, command.Type);

            int graceSeconds = command.Payload?.GraceSeconds ?? 60;
            string message = command.Payload?.Message ?? "Perintah dari sistem manajemen Lab TI Unimal.";

            // If simulation mode is active (for developer testing safety)
            if (config.IsSimulationMode && (command.Type.Equals("shutdown", StringComparison.OrdinalIgnoreCase) || command.Type.Equals("restart", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("[SIMULATION] Perintah {Type} (Grace: {Grace}s) dijalankan dalam Mode Simulasi (komputer tidak akan dimatikan).", command.Type, graceSeconds);
                await Task.Delay(1000); // Simulate execution
                return new CommandExecutionResult
                {
                    Success = true,
                    Message = $"[SIMULASI] Perintah {command.Type} berhasil diterima dan disimulasikan (grace: {graceSeconds}s)."
                };
            }

            switch (command.Type.ToLowerInvariant())
            {
                case "shutdown":
                    return ExecuteWindowsShutdown(graceSeconds, message, isRestart: false);

                case "restart":
                    return ExecuteWindowsShutdown(graceSeconds, message, isRestart: true);

                case "abort_shutdown":
                    return AbortWindowsShutdown();

                case "broadcast":
                    _logger.LogInformation("[BROADCAST DITERIMA] {Message}", message);
                    return new CommandExecutionResult
                    {
                        Success = true,
                        Message = $"Pesan broadcast diterima: {message}"
                    };

                case "kiosk_toggle":
                    if (config.IsSimulationMode)
                    {
                        _logger.LogInformation("[SIMULASI] Kebijakan Kiosk Mode diterapkan: TaskMgr={Tm}, Cmd={Cmd}, Reg={Reg}, CP={Cp}",
                            command.Payload?.DisableTaskmgr, command.Payload?.DisableCmd, command.Payload?.DisableRegedit, command.Payload?.DisableControlPanel);
                        return new CommandExecutionResult
                        {
                            Success = true,
                            Message = "[SIMULASI] Kebijakan Kiosk Mode berhasil disimulasikan."
                        };
                    }

                    if (kioskManager != null && command.Payload != null)
                    {
                        var policy = new KioskPolicy
                        {
                            DisableTaskmgr = command.Payload.DisableTaskmgr,
                            DisableCmd = command.Payload.DisableCmd,
                            DisableRegedit = command.Payload.DisableRegedit,
                            DisableControlPanel = command.Payload.DisableControlPanel
                        };

                        bool applied = kioskManager.ApplyPolicy(policy);
                        _logger.LogInformation("Kebijakan Kiosk Mode diterapkan: TaskMgr={Tm}, Cmd={Cmd}, Reg={Reg}, CP={Cp}",
                            policy.DisableTaskmgr, policy.DisableCmd, policy.DisableRegedit, policy.DisableControlPanel);

                        return new CommandExecutionResult
                        {
                            Success = applied,
                            Message = applied ? "Kebijakan Kiosk Mode berhasil diterapkan ke sistem Windows." : "Gagal menerapkan kebijakan ke registry (perlu hak Administrator / Windows Service)."
                        };
                    }
                    return new CommandExecutionResult { Success = true, Message = "Kiosk toggle diterima." };

                case "cleanup":
                    if (config.IsSimulationMode)
                    {
                        _logger.LogInformation("[SIMULASI] Pembersihan file sampah (%TEMP% & Recycle Bin) berhasil disimulasikan.");
                        return new CommandExecutionResult
                        {
                            Success = true,
                            Message = "[SIMULASI] Pembersihan selesai: Berhasil membebaskan 1.45 GB data sementara."
                        };
                    }

                    var cleanupMgr = new CleanupManager();
                    var cleanRes = cleanupMgr.ExecuteCleanup();
                    _logger.LogInformation("{Message}", cleanRes.Message);
                    return new CommandExecutionResult
                    {
                        Success = cleanRes.Success,
                        Message = cleanRes.Message
                    };

                case "uninstall_software":
                    string swName = command.Payload?.SoftwareName ?? "Aplikasi";
                    string? uninstStr = command.Payload?.UninstallString;

                    if (string.IsNullOrWhiteSpace(uninstStr))
                    {
                        return new CommandExecutionResult
                        {
                            Success = false,
                            Message = $"Tidak ada string uninstaller untuk '{swName}'."
                        };
                    }

                    if (config.IsSimulationMode)
                    {
                        _logger.LogInformation("[SIMULASI] Uninstall '{Software}' disimulasikan dengan string: {String}", swName, uninstStr);
                        return new CommandExecutionResult
                        {
                            Success = true,
                            Message = $"[SIMULASI] Silent uninstall '{swName}' berhasil disimulasikan."
                        };
                    }

                    return ExecuteSilentUninstall(swName, uninstStr);

                case "lock":
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "rundll32.exe",
                            Arguments = "user32.dll,LockWorkStation",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        return new CommandExecutionResult
                        {
                            Success = true,
                            Message = "Layar workstation PC berhasil dikunci (Lock Workstation)."
                        };
                    }
                    catch (Exception ex)
                    {
                        return new CommandExecutionResult
                        {
                            Success = false,
                            Message = $"Gagal mengunci workstation: {ex.Message}"
                        };
                    }

                default:
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Tipe perintah '{command.Type}' belum didukung pada versi Agent ini."
                    };
            }
        }

        private CommandExecutionResult ExecuteSilentUninstall(string softwareName, string rawUninstallString)
        {
            try
            {
                string commandLine = rawUninstallString.Trim();
                string fileName;
                string arguments;

                if (commandLine.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = "msiexec.exe";
                    string guidOrArgs = commandLine.Substring(7).Trim();
                    if (guidOrArgs.StartsWith("/I", StringComparison.OrdinalIgnoreCase))
                    {
                        guidOrArgs = "/x" + guidOrArgs.Substring(2);
                    }
                    else if (!guidOrArgs.StartsWith("/x", StringComparison.OrdinalIgnoreCase))
                    {
                        guidOrArgs = "/x " + guidOrArgs;
                    }
                    arguments = $"{guidOrArgs} /qn /norestart";
                }
                else
                {
                    if (commandLine.StartsWith("\""))
                    {
                        int closingQuote = commandLine.IndexOf('"', 1);
                        if (closingQuote > 1)
                        {
                            fileName = commandLine.Substring(1, closingQuote - 1);
                            arguments = commandLine.Substring(closingQuote + 1).Trim();
                        }
                        else
                        {
                            fileName = commandLine.Trim('"');
                            arguments = "";
                        }
                    }
                    else
                    {
                        int firstSpace = commandLine.IndexOf(' ');
                        if (firstSpace > 0)
                        {
                            fileName = commandLine.Substring(0, firstSpace);
                            arguments = commandLine.Substring(firstSpace + 1).Trim();
                        }
                        else
                        {
                            fileName = commandLine;
                            arguments = "";
                        }
                    }

                    if (!arguments.Contains("/silent", StringComparison.OrdinalIgnoreCase) &&
                        !arguments.Contains("/quiet", StringComparison.OrdinalIgnoreCase) &&
                        !arguments.Contains("/s", StringComparison.OrdinalIgnoreCase))
                    {
                        arguments = $"{arguments} /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /quiet /qn /s".Trim();
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Gagal memulai proses uninstaller untuk '{softwareName}'."
                    };
                }

                bool finished = proc.WaitForExit(180000);
                if (!finished)
                {
                    try { proc.Kill(true); } catch { }
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Proses uninstaller '{softwareName}' melebihi batas waktu (timeout 3 menit)."
                    };
                }

                if (proc.ExitCode == 0 || proc.ExitCode == 3010)
                {
                    return new CommandExecutionResult
                    {
                        Success = true,
                        Message = $"Software '{softwareName}' berhasil di-uninstall secara silent (Exit Code: {proc.ExitCode})."
                    };
                }
                else
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Uninstaller '{softwareName}' selesai dengan kode keluar: {proc.ExitCode}."
                    };
                }
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = $"Exception saat uninstall '{softwareName}': {ex.Message}"
                };
            }
        }

        private CommandExecutionResult ExecuteWindowsShutdown(int graceSeconds, string comment, bool isRestart)
        {
            try
            {
                string flag = isRestart ? "/r" : "/s";
                string sanitizedComment = comment.Replace("\"", "'");
                string args = $"{flag} /t {graceSeconds} /c \"{sanitizedComment}\" /f";

                var psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return new CommandExecutionResult { Success = false, Message = "Gagal menjalankan proses shutdown.exe" };
                }

                process.WaitForExit(5000);
                string stderr = process.StandardError.ReadToEnd();

                if (process.ExitCode == 0)
                {
                    string action = isRestart ? "Restart" : "Shutdown";
                    return new CommandExecutionResult
                    {
                        Success = true,
                        Message = $"{action} dijadwalkan dalam {graceSeconds} detik."
                    };
                }
                else
                {
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"shutdown.exe keluar dengan kode {process.ExitCode}: {stderr}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = $"Exception saat menjalankan shutdown: {ex.Message}"
                };
            }
        }

        private CommandExecutionResult AbortWindowsShutdown()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",
                    Arguments = "/a",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit(3000);

                return new CommandExecutionResult
                {
                    Success = true,
                    Message = "Perintah shutdown/restart dibatalkan."
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Message = $"Gagal membatalkan shutdown: {ex.Message}"
                };
            }
        }
    }
}
