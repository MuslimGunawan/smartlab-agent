using System.Text.Json.Serialization;
using LabAgent.Shared.Services;

namespace LabAgent.Shared.Models
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class RegisterRequest
    {
        [JsonPropertyName("pairing_code")]
        public string PairingCode { get; set; } = string.Empty;

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;

        [JsonPropertyName("mac_address")]
        public string? MacAddress { get; set; }

        [JsonPropertyName("nama_pc")]
        public string? NamaPc { get; set; }
    }

    public class RegisterResponseData
    {
        [JsonPropertyName("device_token")]
        public string DeviceToken { get; set; } = string.Empty;

        [JsonPropertyName("computer_id")]
        public int ComputerId { get; set; }

        [JsonPropertyName("nama_pc")]
        public string NamaPc { get; set; } = string.Empty;

        [JsonPropertyName("lab_id")]
        public int? LabId { get; set; }

        [JsonPropertyName("lab_nama")]
        public string? LabNama { get; set; }
    }

    public class HeartbeatRequest
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "online";

        [JsonPropertyName("ip")]
        public string? Ip { get; set; }

        [JsonPropertyName("active_user")]
        public string? ActiveUser { get; set; }

        [JsonPropertyName("uptime_seconds")]
        public long UptimeSeconds { get; set; }

        [JsonPropertyName("agent_version")]
        public string AgentVersion { get; set; } = "1.0.0";

        [JsonPropertyName("config_version")]
        public int ConfigVersion { get; set; } = 1;
    }

    public class PendingCommand
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // shutdown, restart, broadcast, etc.

        [JsonPropertyName("payload")]
        public CommandPayload? Payload { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }

    public class CommandPayload
    {
        [JsonPropertyName("grace_seconds")]
        public int GraceSeconds { get; set; } = 60;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("disable_taskmgr")]
        public bool DisableTaskmgr { get; set; }

        [JsonPropertyName("disable_cmd")]
        public bool DisableCmd { get; set; }

        [JsonPropertyName("disable_regedit")]
        public bool DisableRegedit { get; set; }

        [JsonPropertyName("disable_control_panel")]
        public bool DisableControlPanel { get; set; }

        [JsonPropertyName("software_id")]
        public int? SoftwareId { get; set; }

        [JsonPropertyName("software_name")]
        public string? SoftwareName { get; set; }

        [JsonPropertyName("uninstall_string")]
        public string? UninstallString { get; set; }

        [JsonPropertyName("clean_temp")]
        public bool CleanTemp { get; set; }

        [JsonPropertyName("clean_recycle_bin")]
        public bool CleanRecycleBin { get; set; }
    }

    public class DiskPartitionItem
    {
        [JsonPropertyName("drive_letter")]
        public string DriveLetter { get; set; } = string.Empty;

        [JsonPropertyName("total_gb")]
        public double TotalGb { get; set; }

        [JsonPropertyName("free_gb")]
        public double FreeGb { get; set; }
    }

    public class HardwareSyncRequest
    {
        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        [JsonPropertyName("processor")]
        public string? Processor { get; set; }

        [JsonPropertyName("ram_gb")]
        public double? RamGb { get; set; }

        [JsonPropertyName("os_version")]
        public string? OsVersion { get; set; }

        [JsonPropertyName("partitions")]
        public List<DiskPartitionItem> Partitions { get; set; } = new();
    }

    public class SoftwareItem
    {
        [JsonPropertyName("nama")]
        public string Nama { get; set; } = string.Empty;

        [JsonPropertyName("versi")]
        public string? Versi { get; set; }

        [JsonPropertyName("tanggal_install")]
        public string? TanggalInstall { get; set; }

        [JsonPropertyName("ukuran_kb")]
        public long? UkuranKb { get; set; }

        [JsonPropertyName("uninstall_string")]
        public string? UninstallString { get; set; }
    }

    public class SoftwareSyncRequest
    {
        [JsonPropertyName("software")]
        public List<SoftwareItem> Software { get; set; } = new();
    }

    public class HeartbeatResponseData
    {
        [JsonPropertyName("computer_id")]
        public int ComputerId { get; set; }

        [JsonPropertyName("nama_pc")]
        public string NamaPc { get; set; } = string.Empty;

        [JsonPropertyName("pending_commands")]
        public List<PendingCommand> PendingCommands { get; set; } = new();

        [JsonPropertyName("config_version_server")]
        public int ConfigVersionServer { get; set; }

        [JsonPropertyName("config_changed")]
        public bool ConfigChanged { get; set; }

        [JsonPropertyName("server_time")]
        public string? ServerTime { get; set; }
    }

    public class CommandAckRequest
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "executed"; // executed, failed

        [JsonPropertyName("result_message")]
        public string? ResultMessage { get; set; }
    }

    public class AgentConfigResponseData
    {
        [JsonPropertyName("computer_id")]
        public int ComputerId { get; set; }

        [JsonPropertyName("lab_id")]
        public int? LabId { get; set; }

        [JsonPropertyName("lab_nama")]
        public string? LabNama { get; set; }

        [JsonPropertyName("schedules")]
        public List<LocalScheduleItem> Schedules { get; set; } = new();

        [JsonPropertyName("blocklist")]
        public List<string> Blocklist { get; set; } = new();

        [JsonPropertyName("kiosk_settings")]
        public KioskPolicy? KioskSettings { get; set; }

        [JsonPropertyName("github_repo")]
        public string? GithubRepo { get; set; }

        [JsonPropertyName("heartbeat_interval_seconds")]
        public int HeartbeatIntervalSeconds { get; set; } = 15;

        [JsonPropertyName("server_time")]
        public string? ServerTime { get; set; }
    }

    public class AgentLocalConfig
    {
        public string ServerBaseUrl { get; set; } = "http://localhost:8000/api/v1";
        public string? DeviceToken { get; set; }
        public int? ComputerId { get; set; }
        public string? ComputerName { get; set; }
        public int? LabId { get; set; }
        public string? LabName { get; set; }
        public int HeartbeatIntervalSeconds { get; set; } = 15;
        public bool IsSimulationMode { get; set; } = false; // When true, does not forcefully shut down the OS during dev testing
    }
}
