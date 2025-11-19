using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class oShuttle
{
    public long ShuttleId { get; set; }

    public string? GustomerName { get; set; }

    public string? Ip { get; set; }

    public string? Port { get; set; }

    public string? ShuttleSize { get; set; }

    public string? Enabled { get; set; }

    public int? Battery { get; set; }

    public string? Status { get; set; }

    public string? LastStation { get; set; }

    public string? BeginStation { get; set; }

    public string? EndStation { get; set; }

    public string? PosX { get; set; }

    public string? PosY { get; set; }

    public string? RobotDir { get; set; }

    public string? MapCode { get; set; }
}
