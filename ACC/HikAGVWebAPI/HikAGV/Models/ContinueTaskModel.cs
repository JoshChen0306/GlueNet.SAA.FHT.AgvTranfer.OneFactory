/// <summary>
/// 繼續執行任務
/// </summary>
public class ContinueTask
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
    public string wbCode { get; set; } = string.Empty;
    public string podCode { get; set; } = string.Empty;
    public string agvCode { get; set; } = string.Empty;
    public string taskCode { get; set; } = string.Empty;
    public string taskSeq { get; set; } = string.Empty;
    public CodePath nextPositionCode { get; set; } = new CodePath();

    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Wb Code] : {wbCode}; [Pod Code] : {podCode}; [AGV Code] : {agvCode}; [Task Code] : {taskCode}; [Task Seq] : {taskSeq}; {nextPositionCode?.ToString()}";
    }
}

/// <summary>
/// 繼續執行任務回應
/// </summary>
public class ContinueTaskAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}