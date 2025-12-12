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
                    filterAreas = areas.Where(item => item.Value == "C" || item.Value == "D");
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

            // 樓層選擇器
            ViewBag.FloorList = new List<SelectListItem>
            {
                new SelectListItem { Value = "1F", Text = "1F" },
                new SelectListItem { Value = "2F", Text = "2F" },
                new SelectListItem { Value = "3F", Text = "3F" },
                new SelectListItem { Value = "4F", Text = "4F" }
            };
            ViewBag.FloorArea = _configuration.GetSection("FloorArea").Get<Dictionary<string, string[]>>();

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
            string status = need["Status"];
            string assignFlag = (area == "C" && btnName == "ConfirmButton") ? "W" : (area == "C" && btnName == "ChangeButton") ? "R" :"";

            if(btnName == "RejectdButton")
            {
                workOrder = (status=="1")?"":_DBContext.oPort.Where(p => p.StationNo == need["EndStation"]).FirstOrDefault()?.WorkOrder?.ToString() ?? "";    
                endStation = _DBContext.oPort.Where(p=>p.Block=="B" && p.UseFlag=="Y" && p.HaveFlag=="0" && (p.BgnToEnd==""||p.BgnToEnd ==null)).FirstOrDefault()?.StationNo.ToString()??"";
                objStation = need["EndStation"];
            }
            if (string.IsNullOrEmpty(endStation)) return BadRequest(new{message = "暫存區無空架" });

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

        public IActionResult ReLogin([FromBody] Dictionary<string,string> need)
        {
            var userId = need["userId"];
            var password = need["password"];

            var result = _DBContext.pUser.Where(u => u.UserId == userId && u.Password == password).FirstOrDefault()?.GroupId?.ToString()??"";
            if( result =="1" || result == "7")
            {
                return Ok();
            }
            else
            {
                return BadRequest();
            }
           
        }
        public IActionResult GetoNeed()
        {
            var result = _DBContext.oNeed;
            return Json(result);
        }

        /// <summary>
        /// 取得所有站點的最新資料（供前端即時查詢使用）
        /// </summary>
        [HttpGet]
        public IActionResult GetAllStations()
        {
            try
            {
                var stations = _DBContext.oPort
                    .Where(p => p.UseFlag == "Y")
                    .Select(p => new
                    {
                        p.Area,
                        p.Port,
                        p.BgnToEnd,
                        Reserve = string.IsNullOrEmpty(p.BgnToEnd) ? "N" : "Y",  // 轉換 BgnToEnd 為 Reserve
                        p.StationNo,
                        p.Block,
                        p.HaveFlag,
                        p.WorkOrder,
                        p.RackId,
                        p.PutTime,
                        p.MachineName
                    })
                    .ToList();

                return Json(stations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "取得站點資料失敗", error = ex.Message });
            }
        }
    }
}
