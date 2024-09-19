/// <summary>
/// 申請回庫倉位 (CTU)
/// </summary>
public class ApplyBin
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
    public string taskCode { get; set; } = string.Empty;
    public string wbCode { get; set; } = string.Empty;
    public string ctnrCode { get; set; } = string.Empty;
    public string method { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Task Code] : {taskCode}; [Wb Code] : {wbCode}; [Container Code] : {ctnrCode}; [Method] : {method}";
    }
}

/// <summary>
/// 申請回庫倉位 (CTU) 回應
/// </summary>
public class ApplyBinAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public ApplyData data { get; set; } = new ApplyData();
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}; {data?.ToString()}";
    }
}

public class ApplyData
{
    public string isAllow { get; set; } = string.Empty;
    public string ctnrType { get; set; } = string.Empty;
    public string posCode { get; set; } = string.Empty;
    public string posType { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[IsAllow] : {isAllow}; [Container Type] : {ctnrType}; [Position Code] : {posCode}; [Position Type] : {posType}";
    }
}