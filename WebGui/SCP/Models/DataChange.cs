using System;
using System.Collections.Generic;

namespace SCP.Models;

public partial class DataChange
{
    public Dictionary<string,object>? insertdata { get; set; }
    public Dictionary<string, object>? updatedata { get; set; }
    public List<string>? deletedata { get; set; }
}
