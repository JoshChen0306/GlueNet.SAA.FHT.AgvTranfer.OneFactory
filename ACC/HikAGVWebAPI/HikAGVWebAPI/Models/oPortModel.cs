public class oPortModel
{
    public string Area { get; set; }
    public string Block { get; set; }
    public string Port { get; set; }
    public string StationNo { get; set; }
    public string InterfaceName { get; set; }
    public string PanelCallShuttle { get; set; }
    public string Priority { get; set; }
    public string ProductionPartNo { get; set; }
    public string UseFlag { get; set; }
    public string RackId { get; set; }
    public string WorkOrder { get; set; }
    public string HaveFlag { get; set; }
    public string Remark { get; set; }
    public string PutTime { get; set; }
    public string BgnToEnd { get; set; }
    public override string ToString()
    {
        return $@"[Area] : {Area}; [Block] : {Block}; [Port] : {Port}; [Station No] : {StationNo}; [Interface Name] : {InterfaceName}; [Panel Call Shuttle] : {PanelCallShuttle}; [Priority] : {Priority}; [Production Part No] : {ProductionPartNo}; [Use Flag] : {UseFlag}; [Rack ID] : {RackId}; [Work Order] : {WorkOrder}; [Have Flag] : {HaveFlag}; [Remark] : {Remark}; [Put Time] : {PutTime}; [Begin To End] : {BgnToEnd}";
    }
}