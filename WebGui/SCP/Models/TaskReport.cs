namespace SCP.Models
{
    public class TaskReport
    {
        public DateTime Date { get; set; }
        public string? AGV { get; set; }
        public string? DayShift { get; set; }
        public string? NightShift { get; set; }
        public string? Total { get; set; }
        public string? ShiftName { get; set; }
        public string? WorkOrder { get; set; }
        public string? BeginStation { get; set; }
        public string? EndStation { get; set; }
        public string? BeginTime { get; set; }
        public string? EndTime { get; set; }
        public string? TotalTime { get; set; }
    }
}
