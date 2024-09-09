/// <summary>
/// 預調度對外接口
/// </summary>
public class PreScheduleTask
{
    /// <summary>
    /// 请求编号，每个请求都要一个唯一编号， 同一个请求重复提交， 使用同一编号 。
    /// </summary>
    public string reqCode { get; set; } = string.Empty;
    /// <summary>
    /// 请求时间截 格式 : “yyyy MM dd HH:mm:ss”
    /// </summary>
    public string reqTime { get; set; } = string.Empty;
    /// <summary>
    /// 客户端编号，如 PDA HCWMS
    /// </summary>
    public string clientCode { get; set; } = string.Empty;
    /// <summary>
    /// 令牌号, 由调度系统颁发。
    /// </summary>
    public string tokenCode { get; set; } = string.Empty;
    public string positionCode { get; set; } = string.Empty;
    public string nextTask { get; set; } = string.Empty;
    public string agvTyp { get; set; } = string.Empty;
    public string priority { get; set; } = string.Empty;//V3.3
    public string useableLayers { get; set; } = string.Empty;
    public string cacheCount { get; set; } = string.Empty;
    public string update { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Position Code] : {positionCode}; [Next Task] : {nextTask}; [AGV Type] : {agvTyp}; [Priority] : {priority}; [Useable Layers] : {useableLayers}; [Cache Count] : {cacheCount}; [Update] : {update}";
    }
}

/// <summary>
/// 預調度對外接口回應
/// </summary>
public class PreScheduleTaskAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    /// <summary>
    /// 自定义返回（返回任务单号）
    /// </summary>
    public string data { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}; [Data] : {data}";
    }
}