/// <summary>
/// 料箱回庫 TPS (CTU+分撥牆)
/// </summary>
public class ReturnPod
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
    public string taskCode { get; set; } = string.Empty;
    public string agvTyp { get; set; } = string.Empty;
    public string returnPodStrategy { get; set; } = string.Empty;
    public string taskTyp { get; set; } = string.Empty;
    public string ctnrCode { get; set; } = string.Empty;
    public string binCode { get; set; } = string.Empty;
    public string srcBinCode { get; set; } = string.Empty;
    public string wbCode { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [Client Code] : {clientCode}; [Token Code] : {tokenCode}; [Task Code] : {taskCode}; [AGV Type] : {agvTyp}; [Return Pod Strategy] : {returnPodStrategy}; [Task Type] : {taskTyp}; [Container Code] : {ctnrCode}; [Bin Code] : {binCode}; [Source Bin Code] : {srcBinCode}; [Wb Code] : {wbCode}";
    }
}

/// <summary>
/// 料箱回庫 TPS (CTU+分撥牆) 回應
/// </summary>
public class ReturnPodAck
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