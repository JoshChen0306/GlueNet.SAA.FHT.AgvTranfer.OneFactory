public class ResultModel
{
    public bool Result { get; set; } = false;
    //public PreRequireModel PreRequire { get; set; }
    //public WIPInfoModel WIPInfo { get; set; }
    public string Message { get; set; } = string.Empty;
    public override string ToString()
    {
        return $"[Result] : {Result}; [Message] : {Message}";
    }
}