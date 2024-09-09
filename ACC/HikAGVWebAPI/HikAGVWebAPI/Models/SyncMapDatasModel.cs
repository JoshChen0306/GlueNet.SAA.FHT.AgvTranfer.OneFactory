using System.Collections.Generic;

/// <summary>
/// 地圖位置信息同步
/// </summary>
public class SyncMapDatas
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
    public string mapDataCode { get; set; } = string.Empty;
    public string mapShortName { get; set; } = string.Empty;
    public string dataTyp { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Map Data Code] : {mapDataCode}; [Map Short Name] : {mapShortName}; [Data Type] : {dataTyp}";
    }
}

/// <summary>
/// 地圖位置信息同步回應
/// </summary>
public class SyncMapDatasAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public List<SyncDatas> data { get; set; } = new List<SyncDatas>();
    public override string ToString()
    {
        string sData = data == null ? string.Empty : string.Join(";", data);
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}; {sData}";
    }
}

public class SyncDatas
{
    public string cooX { get; set; }
    public string cooY { get; set; }
    public string dataTyp { get; set; }
    public string direction { get; set; }
    public string mapCode { get; set; }
    public string mapDataCode { get; set; }
    public string positionCode { get; set; }
    public string berthType { get; set; }
    public override string ToString()
    {
        return $@"[cooX] : {cooX}; [cooY] : {cooY}; [Data Type] : {dataTyp}; [Direction] : {direction}; [Map Code] : {mapCode}; [Map Data Code] : {mapDataCode}; [Position Code] : {positionCode}; [Berth Type] : {berthType}";
    }
}