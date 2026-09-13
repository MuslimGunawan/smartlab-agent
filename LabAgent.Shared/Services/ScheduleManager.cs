using System.Globalization;

namespace LabAgent.Shared.Services
{
    public class LocalScheduleItem
    {
        public int Id { get; set; }
        public string Tipe { get; set; } = "shutdown";
        public string Waktu { get; set; } = "22:00"; // "HH:mm" or "HH:mm:ss"
        public List<string> Hari { get; set; } = new();
    }

    public class ScheduleManager
    {
        private List<LocalScheduleItem> _schedules = new();
        private string _lastExecutedKey = string.Empty;

        public void UpdateSchedules(List<LocalScheduleItem> schedules)
        {
            _schedules = schedules ?? new List<LocalScheduleItem>();
        }

        public LocalScheduleItem? CheckDueSchedule(DateTime now)
        {
            string dayName = GetIndonesianDayName(now.DayOfWeek);
            string currentTime = now.ToString("HH:mm");

            foreach (var sched in _schedules)
            {
                // Normalize schedule time to HH:mm
                string schedTime = sched.Waktu.Length >= 5 ? sched.Waktu.Substring(0, 5) : sched.Waktu;

                if (schedTime == currentTime && sched.Hari.Contains(dayName))
                {
                    string executionKey = $"{sched.Id}_{now:yyyyMMdd_HHmm}";
                    if (_lastExecutedKey != executionKey)
                    {
                        _lastExecutedKey = executionKey;
                        return sched;
                    }
                }
            }

            return null;
        }

        private static string GetIndonesianDayName(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "senin",
                DayOfWeek.Tuesday => "selasa",
                DayOfWeek.Wednesday => "rabu",
                DayOfWeek.Thursday => "kamis",
                DayOfWeek.Friday => "jumat",
                DayOfWeek.Saturday => "sabtu",
                DayOfWeek.Sunday => "minggu",
                _ => "senin"
            };
        }
    }
}
