using System.Collections.Generic;

/// <summary>
/// 生成任務單
/// </summary>
public class SchedulingTask
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
    public string taskTyp { get; set; } = string.Empty;
    public string ctnrTyp { get; set; } = string.Empty;
    public string ctnrCode { get; set; } = string.Empty;
    public string taskMode { get; set; } = string.Empty;//V3.3
    public string wbCode { get; set; } = string.Empty;
    public List<CodePath> positionCodePath { get; set; } = new List<CodePath>();
    public string podCode { get; set; } = string.Empty;
    public string podDir { get; set; } = string.Empty;
    public string podTyp { get; set; } = string.Empty;
    public string materialLot { get; set; } = string.Empty;
    public string priority { get; set;} = string.Empty;
    public string taskCode { get; set; } = string.Empty;
    public string agvCode { get; set; } = string.Empty;
    public string groupId { get; set; } = string.Empty;//V3.3
    public string data { get; set;} = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Task Type] : {taskTyp}; [Container Type] : {ctnrTyp}; [Container Code] : {ctnrCode}; [Task Mode] : {taskMode}; [Wb Code] : {wbCode}; {string.Join("; ", positionCodePath)}; [Pod Code] : {podCode}; [Pod Dir] : {podDir}; [Pod Type] : {podTyp}; [Material Lot] : {materialLot}; [Priority] : {priority}; [Task Code] : {taskCode}; [AGV Code] : {agvCode}; [Group ID] : {groupId}; [Data] : {data}";
    }
}

/// <summary>
/// 位置路徑
/// </summary>
public class CodePath
{
    public string positionCode { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Position Code] : {positionCode}; [Type] : {type}";
    }
}

/// <summary>
/// 生成任務單回應
/// </summary>
public class SchedulingTaskAck
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