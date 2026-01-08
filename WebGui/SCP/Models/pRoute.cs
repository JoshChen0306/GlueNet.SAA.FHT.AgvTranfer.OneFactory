using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class pRoute
{
    public string RouteId { get; set; } = null!;

    public string RouteName { get; set; } = null!;

    public string? SourceFloor { get; set; }

    public string? TargetFloor { get; set; }

    public string? RouteType { get; set; }

    public string? SourceAreas { get; set; }

    public string? TargetAreas { get; set; }

    public string? ControlFlag { get; set; }

    public int? SortOrder { get; set; }

    /// <summary>
    /// 派送模式: DISPATCH=一般派送, RELEASE=Release專用
    /// </summary>
    public string? DispatchMode { get; set; }
}
