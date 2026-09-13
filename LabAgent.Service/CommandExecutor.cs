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

                default:
                    return new CommandExecutionResult
                    {
                        Success = false,
                        Message = $"Tipe perintah '{command.Type}' belum didukung pada versi Agent ini."
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
