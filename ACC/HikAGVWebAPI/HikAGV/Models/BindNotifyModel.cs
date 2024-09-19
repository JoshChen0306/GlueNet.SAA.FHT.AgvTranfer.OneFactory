using System.Collections.Generic;

/// <summary>
/// 綁定解綁通知
/// </summary>
public class BindNotify
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
    public string method { get; set; } = string.Empty;
    public string indBind { get; set; } = string.Empty;
    public List<BindParam> bindParam { get; set; } = new List<BindParam>();
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Method] : {method}; [IndBind] : {indBind}; {string.Join(",", bindParam)}";
    }
}

public class BindParam
{
    public string podCode { get; set; } = string.Empty;
    public string berthCode { get; set; } = string.Empty;
    public string materialLot { get; set; } = string.Empty;
    public string ctnrCode { get; set; } = string.Empty;
    public string ctnrType { get; set; } = string.Empty;
    public string stgBinCode { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Pod Code] : {podCode}; [Berth Code] : {berthCode}; [Material Lot] : {materialLot}; [Container Code] : {ctnrCode}; [Container Type] : {ctnrType}; [Stage Bin Code] : {stgBinCode}";
    }
}

/// <summary>
/// 綁定解綁通知回應
/// </summary>
public class BindNotifyAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public string data { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}; [Data] : {data}";
    }
}