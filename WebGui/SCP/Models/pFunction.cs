using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class pFunction
{
    public short? ColumnNo { get; set; }

    public short? RowNo { get; set; }

    public short? FunctionNo { get; set; }

    public string? FunctionType { get; set; }

    public string? FunctionEnglishName { get; set; }

    public string? FunctionChineseName { get; set; }

    public string? ControlFlag { get; set; }

    public string? WebUrl { get; set; }

    public string? WebIcon { get; set; }
}
