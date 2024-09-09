using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oNeed
{
    public string ObjStation { get; set; } = null!;

    public string? RackId { get; set; }

    public string? WorkOrder { get; set; }

    public string? EndStation { get; set; }

    public string? TaskSource { get; set; }

    public string? TaskDateTime { get; set; }

    public string? AssignFlag { get; set; }
}
