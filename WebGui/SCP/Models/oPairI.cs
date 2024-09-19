using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oPairI
{
    public string TaskDateTime { get; set; } = null!;

    public string ObjStation { get; set; } = null!;

    public string? PairType { get; set; }

    public string? PairIO { get; set; }

    public string? PairSource { get; set; }

    public string? PairFlag { get; set; }

    public string? PairStation { get; set; }
}
