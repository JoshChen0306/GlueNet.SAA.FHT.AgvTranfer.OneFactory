namespace SCP.Models
{
    public class ActivationReport
    {
        public string ShuttleId { get; set; }
        public double Travling { get; set; }
        public double Idle { get; set; }
        public double Charging { get; set; }
        public double Alarm { get; set; }
        public double Offline { get; set; }
        public double NonTraveling { get; set; }
        public double Activation { get; set; }
    }
}
