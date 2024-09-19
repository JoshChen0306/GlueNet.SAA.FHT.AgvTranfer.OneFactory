public class ubMissionModel
{
    public string TaskDateTime { get; set; }
    public string SerialNo { get; set; }
    public string BeginStation { get; set; }
    public string EndStation { get; set; }
    public string TaskSource { get; set; }
    public string TaskCode { get; set; }
    public string ShuttleId { get; set; }
    public string RackId { get; set; }
    public string WorkOrder { get; set; }
    public string Remark1 { get; set; }
    public string Remark2 { get; set; }
    public string Remark3 { get; set; }
    public string OkFlag { get; set; }
    public string BeginTime { get; set; }
    public string EndTime { get; set; }
    public override string ToString()
    {
        return $@"[Task Date Time] : {TaskDateTime}; [Serial No] : {SerialNo}; [Begin Station] : {BeginStation}; [End Station] : {EndStation}; [Task Source] : {TaskSource}; [Task Code] : {TaskCode}; [Shuttle ID] : {ShuttleId}; [Rack ID] : {RackId}; [Work Order] : {WorkOrder}; [Remark1] : {Remark1}; [Remark2] : {Remark2}; [Remark3] : {Remark3}; [Ok Flag] : {OkFlag}; [Begin Time] : {BeginTime}; [End Time] : {EndTime}";
    }
}