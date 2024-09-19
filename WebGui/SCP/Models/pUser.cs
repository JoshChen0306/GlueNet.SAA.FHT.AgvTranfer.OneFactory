using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class pUser
{
    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string? GroupId { get; set; }

    public string? Mail { get; set; }

    public string? Tel { get; set; }

    public string? ModifiedTime { get; set; }
}
