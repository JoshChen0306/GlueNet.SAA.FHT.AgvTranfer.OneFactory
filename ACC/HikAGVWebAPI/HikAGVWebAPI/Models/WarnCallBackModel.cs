using System.Collections.Generic;

/// <summary>
/// 告警推送通知
/// </summary>
public class WarnCallBack
{
    /// <summary>
    /// 请求编号，每个请求都要一个唯一编号， 同一个请求重复提交， 使用同一编号 。
    /// </summary>
    public string reqCode { get; set; } = string.Empty;
    /// <summary>
    /// 请求时间截 格式 : “yyyy MM dd HH:mm:ss”
    /// </summary>
    public string reqTime { get; set; } = string.Empty;
    public string clientCode { get; set; } = string.Empty;
    public string tokenCode { get; set; } = string.Empty;
    public List<WarnData> data { get; set; } = new List<WarnData>();
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; {string.Join(",", data)}";
    }
}

public class WarnData
{
    public string robotCode { get; set; } = string.Empty;
    public string beginTime { get; set; } = string.Empty;
    public string warnContent { get; set; } = string.Empty;
    public string taskCode { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Robot Code] : {robotCode}; [Begin Time] : {beginTime}; [Warn Content] : {warnContent}; [Task Code] : {taskCode}";
    }
}

/// <summary>
/// 告警推送通知回應
/// </summary>
public class WarnCallBackAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}