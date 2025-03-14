namespace SCP.Models
{
    public class ActivationReport
    {
        public string ShuttleId { get; set; }
        public string Travling { get; set; }
        public string Idle { get; set; }
        public string Charging { get; set; }
        public string Alarm { get; set; }
        public string Offline { get; set; }
        public string NonTraveling { get; set; }
        public string Activation { get; set; }
        public string Date { get; set; }
    }
}
