public class oRequireModel
{
    public string TaskDateTime { get; set; }
    public string ObjStation { get; set; }
    public string SerialNo { get; set; }
    public string BeginStation { get; set; }
    public string EndStation { get; set; }
    public string TaskSource { get; set; }
    public string RackId { get; set; }
    public string WorkOrder { get; set; }
    public string AssignFlag { get; set; }
    public string OkFlag { get; set; }
    public override string ToString()
    {
        return $@"[Task Date Time] : {TaskDateTime}; [ObjStation] : {ObjStation}; [Serial No] : {SerialNo}; [Begin Station] : {BeginStation}; [End Station] : {EndStation}; [Task Source] : {TaskSource}; [Rack ID] : {RackId}; [Work Order] : {WorkOrder}; [Assign Flag] : {AssignFlag}; [OK Flag] : {OkFlag}";
    }
}