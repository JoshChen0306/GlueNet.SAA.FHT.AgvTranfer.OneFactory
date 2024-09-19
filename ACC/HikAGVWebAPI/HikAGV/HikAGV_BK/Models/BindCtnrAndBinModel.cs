/// <summary>
/// 容器與倉位綁定、解綁
/// </summary>
public class CtnrAndBin
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
    public string ctnrCode { get; set; } = string.Empty;
    public string ctnrTyp { get; set; } = string.Empty;
    public string stgBinCode { get; set; } = string.Empty;
    public string binName { get; set; } = string.Empty;
    public string characterValue { get; set; } = string.Empty;
    public string positionCode { get; set; } = string.Empty;
    public string indBind { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Container Code] : {ctnrCode}; [Container Type] : {ctnrTyp}; [Stage Bin Code] : {stgBinCode}; [Bin Name] : {binName}; [Character Value] : {characterValue}; [Position Code] : {positionCode}; [IndBind] : {indBind}";
    }
}

/// <summary>
/// 容器與倉位綁定、解綁回應
/// </summary>
public class CtnrAndBinAck
{
    public string code { get; set; }
    public string data { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Data] : {data}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}