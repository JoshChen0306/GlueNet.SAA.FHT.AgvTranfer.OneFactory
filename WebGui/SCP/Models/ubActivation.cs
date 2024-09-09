using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class ubActivation
{
    public string TaskDateTime { get; set; } = null!;

    public string ShuttleStation { get; set; } = null!;

    public string BeginStation { get; set; } = null!;

    public string EndStation { get; set; } = null!;

    public string ReceivingTime { get; set; } = null!;

    public string? BeginTime { get; set; }

    public string? EndTime { get; set; }

    public string? ShuttleId { get; set; }

    public string? TaskType { get; set; }
}
