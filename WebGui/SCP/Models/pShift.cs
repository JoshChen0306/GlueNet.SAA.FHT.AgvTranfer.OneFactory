using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class pShift
{
    public string? ShiftCode { get; set; }

    public string? ShiftName { get; set; }

    public string? BeginDateTime { get; set; }

    public string? EndDateTime { get; set; }

    public string? EffectDateTime { get; set; }

    public string? ModifiedTime { get; set; }
}
