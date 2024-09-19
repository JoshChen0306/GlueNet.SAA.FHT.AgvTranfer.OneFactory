using System.Collections.Generic;

/// <summary>
/// 查詢任務狀態
/// </summary>
public class TaskStatus
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
    public string[] taskCodes { get; set; } = new string[0]; 
    public string agvCode { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Task Codes] : {string.Join(",", taskCodes)}; [AGV Code] : {agvCode}";
    }
}

/// <summary>
/// 任務列表
/// </summary>
public class TaskData
{
    public string taskCode { get; set; }
    public string taskTyp { get; set; }
    public string taskStatus { get; set; }
    public string agvCode { get; set; }
    public override string ToString()
    {
        return $@"[Task Code] : {taskCode}; [Task Type] : {taskTyp}; [Task Status] : {taskStatus}; [AGV Code] : {agvCode}";
    }
}

/// <summary>
/// 查詢任務狀態回應
/// </summary>
public class TaskStatusAck
{
    public string code { get; set; }
    public List<TaskData> data { get; set; } = new List<TaskData>();
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        string sData = data == null ? string.Empty : string.Join(";", data);
        return $@"[Code] : {code}; {sData}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}