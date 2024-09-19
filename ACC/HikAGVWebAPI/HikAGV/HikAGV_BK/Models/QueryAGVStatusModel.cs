using System.Collections.Generic;

/// <summary>
/// 查詢 AGV 狀態
/// </summary>
public class AGVStatus
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
    public string mapCode { get; set; } = string.Empty;

    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Map Code] : {mapCode}";
    }
}

/// <summary>
/// AGV 資料
/// </summary>
public class AGVStatusData
{
    public string robotCode { get; set; }
    public string robotDir { get; set; }
    public string robotIp { get; set; }
    public string battery { get; set; }
    public string posX { get; set; }
    public string posY { get; set; }
    public string mapCode { get; set; }
    public string speed { get; set; }
    public string status { get; set; }
    public string exclType { get; set; }
    public string stop { get; set; }
    public string podCode { get; set; }
    public string podDir { get; set; }
    public string[] path { get; set; } = new string[0];
    public override string ToString()
    {
        return $@"[Robot Code] : {robotCode}; [Robot Dir] : {robotDir}; [Robot IP] : {robotIp}; [Battery] : {battery} ; [Pos X] : {posX}; [Pos Y] : {posY}; [Map Code] : {mapCode}; [Speed] : {speed}; [Status] : {status} ; [Excl Type] : {exclType}; [Stop] : {stop}; [Pod Code] : {podCode}; [Pod Dir] : {podDir}; [Path] : {string.Join(",", path)}";
    }
}

/// <summary>
/// 查詢 AGV 狀態回應
/// </summary>
public class AGVStatusAck
{
    public string code { get; set; }
    public List<AGVStatusData> data { get; set; } = new List<AGVStatusData>();
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        string sData = data == null ? string.Empty : string.Join(";", data);
        return $@"[Code] : {code}; {sData}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}