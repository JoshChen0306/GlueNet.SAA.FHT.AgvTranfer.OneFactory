public class oNeedModel
{
    public string TaskDateTime { get; set; }
    public string ObjStation { get; set; }
    public string RackId { get; set; }
    public string WorkOrder { get; set; }
    public string EndStation { get; set; }
    public string TaskSource { get; set; }
    public string AssignFlag { get; set; }
    public override string ToString()
    {
        return $@"[Task Date Time] : {TaskDateTime}; [ObjStation] : {ObjStation}; [Rack ID] : {RackId}; [Work Order] : {WorkOrder}; [Task Source] : {TaskSource}; [Assign Flag] : {AssignFlag}";
    }
}