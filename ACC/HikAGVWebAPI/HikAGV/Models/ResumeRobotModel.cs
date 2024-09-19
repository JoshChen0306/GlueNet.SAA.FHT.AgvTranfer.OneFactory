/// <summary>
/// 恢復 AGV
/// </summary>
public class ResumeRobot
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
    public string tokenCode { get; set; } = string.Empty;
    public string robotCount { get; set; } = string.Empty;
    public string mapShortName { get; set; } = string.Empty;
    public string[] robots { get; set; }
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Robot Count] : {robotCount}; [Map Short Name] : {mapShortName}; [Robots] : {string.Join(",", robots)}";
    }
}

/// <summary>
/// 恢復 AGV 回應
/// </summary>
public class ResumeRobotAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}