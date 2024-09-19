using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SCP.Models;
using System.Net.NetworkInformation;

namespace SCP.Controllers
{
    public class DispatchController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public DispatchController(IConfiguration configuration, agvDB_1400004Context DBContext)
        {
            _DBContext= DBContext;
            _configuration = configuration;

        }
        public IActionResult Index()
        {
            var areas = _configuration.GetSection("Area").Get<Dictionary<string, string>>();
            var areaList = new List<SelectListItem>();
            
            foreach (var item in areas)
            {
                areaList.Add(new SelectListItem { Value = item.Value, Text = item.Key });
            }      

            ViewBag.AreaList = areaList;
            ViewBag.Site = _DBContext.oPort.Select(t => new SelectListItem { Value = t.StationNo, Text = t.StationNo });
            return View();
        }

        public IActionResult UpdateDispatch()
        {
            ViewBag.Dispatchs = _DBContext.oRequire
                .ToList()
                .Select(item=> new
                {
                    item.BeginStation,
                    item.EndStation,
                    WorkOrder = item.WorkOrder.Split('^').Length > 3 ? item.WorkOrder.Split('^')[3] : string.Empty,                   
                    Status = item.AssignFlag == "Y" && item.OkFlag =="R" ? "執行中": item.AssignFlag == "Y" ? "已派車" : item.AssignFlag == "C" ? "取消" : "異常",
                    TextColor = item.AssignFlag == "Y" && item.OkFlag == "R" ? "text-primary" : item.AssignFlag == "Y" ? "text-success" : item.AssignFlag == "C" ? "text-secondary" : "text-danger"
                });
            return PartialView("_DispatchPartial");
        }

        public IActionResult InsertoNeed([FromBody] List<Dictionary<string ,string>> need )
        {
            Dictionary<string,string> needDict = need.ToDictionary(item => item["name"], item => item["value"]);

            string area = needDict["Area"];
            string objStation = needDict["BegingStation"];
            string endStation = needDict["EndStation"];
            string rackId = needDict["RackId"];
            string workOrder = needDict["WorkOrder"];
            string assignFlag = area == "C" ? "W" : "";

            string sql = "INSERT INTO oNeed (ObjStation,RackId,WorkOrder,EndStation,TaskSource,TaskDateTime,AssignFlag) VALUES({0},{1},{2},{3},{4},{5},{6})";
            try
            {
                _DBContext.Database.ExecuteSqlRaw(sql,objStation, rackId, workOrder,endStation,"Web",DateTime.Now.ToString("yyyyMMddHHmmssffffff"),assignFlag);
            }
            catch(Exception ex)
            {

            }

            return Ok();
        }

        public IActionResult UpdateoPort([FromBody] List<Dictionary<string, string>> need)
        {
            Dictionary<string, string> needDict = need.ToDictionary(item => item["name"], item => item["value"]);
            string objStation = needDict["BegingStation"];

            try
            {
                _DBContext.oPort
                    .Where(p => p.StationNo == objStation)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.WorkOrder, "")
                        .SetProperty(p => p.HaveFlag, "1"));
            }
            catch (Exception ex)
            {

            }

            return Ok();
        }
    }
}
