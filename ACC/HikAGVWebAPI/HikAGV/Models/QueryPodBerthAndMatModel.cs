using System.Collections.Generic;

/// <summary>
/// 查詢貨架儲位與物料批次關係
/// </summary>
public class QryPodBerthAndMat
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
    public string podCode { get; set; } = string.Empty;
    public string materialLot { get; set; } = string.Empty;
    public string positionCode { get; set; } = string.Empty;
    public string areaCode { get; set; } = string.Empty;
    public string mapShortName { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Pod Code] : {podCode}; [Material Lot] : {materialLot}; [Position Code] : {positionCode}; [Area Code] : {areaCode}; [Map Short Name] : {mapShortName}";
    }
}

/// <summary>
/// 查詢貨架儲位與物料批次關係回應
/// </summary>
public class QryPodBerthAndMatAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public List<QueryPod> data { get; set; } = new List<QueryPod>();

    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}; {string.Join(";", data)}";
    }
}

public class QueryPod
{
    public string areaCode { get; set; } = string.Empty;
    public string materialLot { get; set; } = string.Empty;
    public string podCode { get; set; } = string.Empty;
    public string mapDataCode { get; set; } = string.Empty;
    public string positionCode { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Area Code] : {areaCode}; [Material Lot] : {materialLot}; [Pod Code] : {podCode}; [Map Data Code] : {mapDataCode}; [Position Code] : {positionCode}";
    }
}