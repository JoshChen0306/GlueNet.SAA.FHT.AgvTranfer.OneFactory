using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SCP.Models;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text.RegularExpressions;

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
        [Authorize]
        public IActionResult Index()
        {
            var areas = _configuration.GetSection("Area").Get<Dictionary<string, string>>();
            var areaList = new List<SelectListItem>();
            IEnumerable<KeyValuePair<string, string>> filterAreas = areas;
            string groupId = User.FindFirst(ClaimTypes.Role)?.Value;

            switch (groupId)
            {
                case "2":
                    filterAreas = areas.Where(item => item.Value=="A");
                    break;
                case "6":
                    filterAreas = areas.Where(item => item.Value == "C"|| item.Value == "D");
                    break;
                case "5":
                    filterAreas = areas.Where(item => item.Value == "F");
                    break;
            }
                


            foreach (var item in filterAreas)
            {
                areaList.Add(new SelectListItem { Value = item.Value, Text = item.Key });
            }      

            ViewBag.AreaList = areaList;
            ViewBag.Site = _DBContext.oPort.Where(p=>p.UseFlag =="Y").Select(p => new SelectListItem { Value = p.StationNo, Text = p.MachineName });
            ViewBag.Role = groupId;
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
            ViewBag.Role = User.FindFirst(ClaimTypes.Role)?.Value;
            return PartialView("_DispatchPartial");
        }

        public IActionResult InsertoNeed([FromBody] Dictionary<string ,string> need)
        {
            
            string area = need["Area"];
            string objStation = need["BegingStation"];
            string endStation = need["EndStation"];
            string rackId = need["RackId"];
            string workOrder = need["WorkOrder"];
            string btnName = need["btnName"];
            string assignFlag = (area == "C" && btnName == "ConfirmButton") ? "W" : (area == "C" && btnName == "RejectdButton") ? "R" :"";

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

        public IActionResult UpdateoPort([FromBody]Dictionary<string, string> need)
        {
           
            string objStation = need["BegingStation"];

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

        public IActionResult DeleteoNeed([FromBody] Dictionary<string,string> need)
        {
            string begingStation = need["beginStation"];
            string endStation = need["endStation"];

            try
            {
                _DBContext.oRequire
                    .Where(p => p.BeginStation == begingStation && p.EndStation == endStation)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.OkFlag, "C"));
                _DBContext.oMission
                   .Where(p => p.BeginStation == begingStation && p.EndStation == endStation)
                   .ExecuteUpdate(setters => setters
                       .SetProperty(p => p.OkFlag, "C"));
            }
            catch (Exception ex)
            {

            }
            return Ok();
        }
        public IActionResult GetoNeed()
        {
            var result = _DBContext.oNeed;
            return Json(result);
        }
    }
}
