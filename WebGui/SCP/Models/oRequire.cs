using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oRequire
{
    public string TaskDateTime { get; set; } = null!;

    public string ObjStation { get; set; } = null!;

    public long SerialNo { get; set; }

    public string? BeginStation { get; set; }

    public string? EndStation { get; set; }

    public string? TaskSource { get; set; }

    public string? RackId { get; set; }

    public string? WorkOrder { get; set; }

    public string? AssignFlag { get; set; }

    public string? OkFlag { get; set; }
}
