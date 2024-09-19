using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oPort
{
    public string? Area { get; set; }

    public string? Block { get; set; }

    public int? Port { get; set; }

    public string StationNo { get; set; } = null!;

    public string? InterfaceName { get; set; }

    public string? PanelCallShuttle { get; set; }

    public int? Priority { get; set; }

    public string? ProductionPartNo { get; set; }

    public string? UseFlag { get; set; }

    public string? RackId { get; set; }

    public string? WorkOrder { get; set; }

    public string? HaveFlag { get; set; }

    public string? Remark { get; set; }

    public string? PutTime { get; set; }

    public string? BgnToEnd { get; set; }
}
