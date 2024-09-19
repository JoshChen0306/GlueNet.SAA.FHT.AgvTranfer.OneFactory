/// <summary>
/// 區域清空/釋放
/// </summary>
public class BlockArea
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
    public string matterArea { get; set; } = string.Empty;
    public string indBind { get; set; } = string.Empty;
    public string pause { get; set; } = string.Empty;
    public string controlMod { get; set; } = string.Empty;
    public string targetArea { get; set; } = string.Empty;
    public string noticeThird { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Matter Area] : {matterArea}; [IndBind] : {indBind}; [Pause] : {pause}; [Control Mode] : {controlMod}; [Target Area] : {targetArea}; [Notice Third] : {noticeThird}";
    }
}

/// <summary>
/// 區域清空/釋放回應
/// </summary>
public class BlockAreaAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}