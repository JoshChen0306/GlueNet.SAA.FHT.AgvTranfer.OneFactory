/// <summary>
/// 稼動率 Model
/// </summary>
public class ubActivationModel
{
    /// <summary>
    /// 任務時間
    /// </summary>
    public string TaskDateTime { get; set; }
    /// <summary>
    /// AGV 車號
    /// </summary>
    public string ShuttleId { get; set; }
    /// <summary>
    /// 任務類型
    /// </summary>
    public string TaskType { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public string ShuttleStation { get; set; }
    /// <summary>
    /// 起點
    /// </summary>
    public string BeginStation { get; set; }
    /// <summary>
    /// 終點
    /// </summary>
    public string EndStation { get; set; }
    /// <summary>
    /// 接收時間
    /// </summary>
    public string ReceivingTime { get; set; }
    /// <summary>
    /// 開始時間
    /// </summary>
    public string BeginTime { get; set; }
    /// <summary>
    /// 結束時間
    /// </summary>
    public string EndTime { get; set; }
    public override string ToString()
    {
        return $@"[Task Date Time] : {TaskDateTime}; [Shuttle ID] : {ShuttleId}; [Task Type] : {TaskType}; [Shuttle Station] : {ShuttleStation}; [Begin Station] : {BeginStation}; [End Station] : {EndStation}; [Receiving Time] : {ReceivingTime}; [Begin Time] : {BeginTime}; [End Time] : {EndTime}";
    }
}