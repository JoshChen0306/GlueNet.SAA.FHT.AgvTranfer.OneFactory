/// <summary>
/// 任務執行通知
/// </summary>
public class CallBack
{
    /// <summary>
    /// 请求编号，每个请求都要一个唯一编号， 同一个请求重复提交， 使用同一编号 。
    /// </summary>
    public string reqCode { get; set; } = string.Empty;
    /// <summary>
    /// 请求时间截 格式 : “yyyy MM dd HH:mm:ss”
    /// </summary>
    public string reqTime { get; set; } = string.Empty;
    public string cooX { get; set; } = string.Empty;
    public string cooY { get; set; } = string.Empty;
    public string currentPositionCode { get; set; } = string.Empty;
    public string data { get; set; } = string.Empty;
    public string mapCode { get; set; } = string.Empty;
    public string mapDataCode { get; set; } = string.Empty;
    public string stgBinCode { get; set; } = string.Empty;
    public string method { get; set; } = string.Empty;
    public string podCode { get; set; } = string.Empty;
    public string podDir { get; set; } = string.Empty;
    public string materialLot { get; set; } = string.Empty;
    public string robotCode { get; set; } = string.Empty;
    public string taskCode { get; set; } = string.Empty;
    public string wbCode { get; set; } = string.Empty;
    public string ctnrCode { get; set; } = string.Empty;
    public string ctnrType { get; set; } = string.Empty;
    public string roadWayCode { get; set; } = string.Empty;
    public string seq { get; set; } = string.Empty;
    public string eqpCode { get; set; } = string.Empty;
    public override string ToString()
    {
        return $@"[Request Code] : {reqCode}; [Request Time] : {reqTime}; [cooX] : {cooX}; [cooY] : {cooY}; [Current Position Code] : {currentPositionCode}; [Data] : {data}; [Map Code] : {mapCode}; [Map Data Code] : {mapDataCode}; [Stg Bin Code] : {stgBinCode}; [Method] : {method}; [Pod Code] : {podCode}; [Pod Dir] : {podDir}; [Material Lot] : {materialLot}; [Robot Code] : {robotCode}; [Task Code] : {taskCode}; [Wb Code] : {wbCode}; [Container Code] : {ctnrCode}; [Container Type] : {ctnrType}; [Road Way Code] : {roadWayCode}; [Seq] : {seq}; [Eqp Code] : {eqpCode}";
    }
}

/// <summary>
/// 任務執行通知回應
/// </summary>
public class CallBackAck
{
    public string code { get; set; }
    public string message { get; set; }
    public string reqCode { get; set; }
    public override string ToString()
    {
        return $@"[Code] : {code}; [Message] : {message}; [Request Code] : {reqCode}";
    }
}