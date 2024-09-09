using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class pGroup
{
    public string GroupId { get; set; } = null!;

    public string? GroupEName { get; set; }

    public string? GroupCName { get; set; }

    public int? LogoutTime { get; set; }

    public string? FunctionGroup { get; set; }

    public string? ModifiedTime { get; set; }
}
