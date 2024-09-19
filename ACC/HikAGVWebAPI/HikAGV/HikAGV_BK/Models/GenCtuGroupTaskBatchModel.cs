using System.Collections.Generic;

/// <summary>
/// 料箱順序出庫 (CTU)
/// </summary>
public class GroupTaskBatch
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
    public string seqTyp { get; set; } = string.Empty;
    public List<TaskGroups> taskGroups { get; set; } = new List<TaskGroups>();
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Task Type] : {taskTyp}; [Sequence Type] : {seqTyp}; [Data] : {string.Join(",", taskGroups)}";
    }
}

/// <summary>
/// 料箱資料
/// </summary>
public class TaskGroups
{
    public string taskCode { get; set; } = string.Empty;
    public string ctnrCode { get; set; } = string.Empty;
    public string ctnrTyp { get; set; } = string.Empty;
    public string wbCode { get; set; } = string.Empty;
    public string wbTyp { get; set; } = string.Empty;
    public string groupId { get; set; } = string.Empty;
    public string sequence { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Task Code] : {taskCode}; [Container Code] : {ctnrCode}; [Container Type] : {ctnrTyp}; [Wb Code] : {wbCode}; [Wb Type] : {wbTyp}; [Group ID] : {groupId}; [Sequence] : {sequence}";
    }
}

/// <summary>
/// 料箱順序出庫 (CTU) 回應
/// </summary>
public class GroupTaskBatchAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}