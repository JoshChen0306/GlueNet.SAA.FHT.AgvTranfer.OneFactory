/// <summary>
/// 指定料箱終點 (CTU)
/// </summary>
public class ContainerDestination
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
    public string positionCode { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Container Code] : {ctnrCode}; [Container Type] : {ctnrTyp}; [Position Code] : {positionCode}; [Type] : {type}";
    }
}

/// <summary>
/// 指定料箱終點 (CTU) 回應
/// </summary>
public class ContainerDestinationAck
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