using System.Collections.Generic;

/// <summary>
/// 任務優先權設置
/// </summary>
public class TaskPriority
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
    public List<Priorities> priorities { get; set; } = new List<Priorities>();

    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; {string.Join(",", priorities)}";
    }
}

/// <summary>
/// 任務優先權設置回應
/// </summary>
public class Priorities
{
    public string taskCode { get; set; }
    public string priority { get; set; }
    public override string ToString()
    {
        return $@"[Task Code] : {taskCode}; [Priority] : {priority}";
    }
}

public class TaskPriorityAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}