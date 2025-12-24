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
                // 取得已有待處理任務的起點站，避免重複派送
                // 1. oNeed 中待處理的任務（AssignFlag 為空）
                var pendingFromONeed = _DBContext.oNeed
                    .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                    .Select(n => n.ObjStation)
                    .ToList();

                // 2. oRequire 中進行中的任務（OkFlag 為空，表示尚未完成）
                var pendingFromORequire = _DBContext.oRequire
                    .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                    .Select(r => r.BeginStation)
                    .ToList();

                // 3. oMission 中進行中的任務（OkFlag 為空或 R=執行中）
                var pendingFromOMission = _DBContext.oMission
                    .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                    .Select(m => m.BeginStation)
                    .ToList();

                // 合併所有進行中任務的起點站
                var allPendingBeginStations = pendingFromONeed
                    .Concat(pendingFromORequire)
                    .Concat(pendingFromOMission)
                    .Distinct()
                    .ToList();

                // 取得已有待處理任務的終點站，避免重複指派
                var pendingEndFromONeed = _DBContext.oNeed
                    .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                    .Select(n => n.EndStation)
                    .ToList();

                var pendingEndFromORequire = _DBContext.oRequire
                    .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                    .Select(r => r.EndStation)
                    .ToList();

                var pendingEndFromOMission = _DBContext.oMission
                    .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                    .Select(m => m.EndStation)
                    .ToList();

                // 合併所有進行中任務的終點站
                var allPendingEndStations = pendingEndFromONeed
                    .Concat(pendingEndFromORequire)
                    .Concat(pendingEndFromOMission)
                    .Distinct()
                    .ToList();

                // 合併所有不可選的站點（起點 + 終點）
                var allExcludedStations = allPendingBeginStations
                    .Concat(allPendingEndStations)
                    .Distinct()
                    .ToList();

                var stations = _DBContext.oPort
                    .Where(p => p.UseFlag == "Y" && 
                                !allExcludedStations.Contains(p.StationNo))  // 排除所有進行中任務的站點（起點和終點）
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

        /// <summary>
        /// 登記帳料 - 在空架站點登記工單和貨架資訊
        /// </summary>
        [HttpPost]
        public IActionResult RegisterLot([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";
                string workOrder = data.ContainsKey("workOrder") ? data["workOrder"] : "";
                string rackId = data.ContainsKey("rackId") ? data["rackId"] : "";
                string isVcutMaterial = data.ContainsKey("isVcutMaterial") ? data["isVcutMaterial"] : "false";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 驗證：工單必填
                if (string.IsNullOrEmpty(workOrder))
                {
                    return BadRequest(new { message = "請輸入工單條碼" });
                }

                // 驗證：J 區（3F 插針室）RackId 必填
                var stationArea = stationNo.Substring(0, 1).ToUpper();
                if (stationArea == "J" && string.IsNullOrEmpty(rackId))
                {
                    return BadRequest(new { message = "請輸入貨架條碼" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查該站點是否為待處理任務的終點（避免與 Release 回送任務衝突）
                var pendingAsEndTask = _DBContext.oNeed
                    .FirstOrDefault(n => n.EndStation == stationNo && 
                                         (n.AssignFlag == null || n.AssignFlag == ""));
                if (pendingAsEndTask != null)
                {
                    return BadRequest(new { message = $"此站點有待處理的回送任務（來自 {pendingAsEndTask.ObjStation}），請等待任務完成後再登記" });
                }

                // 檢查該站點是否為進行中任務的起點（不能修改有任務的站點物料）
                var hasPendingAsBegin = 
                    _DBContext.oNeed.Any(n => n.ObjStation == stationNo && (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")) ||
                    _DBContext.oRequire.Any(r => r.BeginStation == stationNo && (r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")) ||
                    _DBContext.oMission.Any(m => m.BeginStation == stationNo && (m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R"));
                
                if (hasPendingAsBegin)
                {
                    return BadRequest(new { message = "此站點有進行中的派送任務，無法修改物料資訊" });
                }

                // 檢查站點狀態（僅記錄，不阻擋）
                if (port.HaveFlag != "0")
                {
                    // 站點不是空架，但仍允許覆蓋登記
                    // 可在此處記錄日誌
                }



                // 處理 V Cut 標記
                // 先移除現有的 ^VCUT 和 ^DONE 標記（如果有的話）
                workOrder = workOrder.Replace("^VCUT^DONE", "").Replace("^VCUT", "").Replace("^DONE", "");
                
                // 判斷站點區域（stationArea 已在前面宣告）
                
                if (stationArea == "T")
                {
                    // T 區（V Cut區）建立物料時，自動標記為已加工完成
                    workOrder = workOrder + "^VCUT^DONE";
                }
                else if (stationArea == "M" && isVcutMaterial == "true")
                {
                    // M 區（雷雕區）勾選 V Cut 專用時，附加 ^VCUT 標記
                    workOrder = workOrder + "^VCUT";
                }

                // 更新 oPort 表
                var putTimeStr = DateTime.Now.ToString("yyyyMMddHHmmssffffff");
                _DBContext.oPort
                    .Where(p => p.StationNo == stationNo)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.HaveFlag, "3")           // 設為料盤
                        .SetProperty(p => p.WorkOrder, workOrder)     // 工單資訊
                        .SetProperty(p => p.RackId, rackId)           // 貨架編號
                        .SetProperty(p => p.PutTime, putTimeStr));    // 放置時間

                return Ok(new { message = "登記成功", stationNo = stationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "登記失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 清除物料 - 將站點設為空架，清除工單和貨架資訊
        /// </summary>
        [HttpPost]
        public IActionResult ClearLot([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查該站點是否有進行中的派送任務（不能清除有任務的站點）
                var hasPendingTask = 
                    _DBContext.oNeed.Any(n => n.ObjStation == stationNo && (n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")) ||
                    _DBContext.oRequire.Any(r => r.BeginStation == stationNo && (r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")) ||
                    _DBContext.oMission.Any(m => m.BeginStation == stationNo && (m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R"));
                
                if (hasPendingTask)
                {
                    return BadRequest(new { message = "此站點有進行中的派送任務，無法清除物料" });
                }

                // 更新 oPort 表 - 清除物料資訊
                _DBContext.oPort
                    .Where(p => p.StationNo == stationNo)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.HaveFlag, "0")      // 設為空架
                        .SetProperty(p => p.WorkOrder, "")      // 清除工單
                        .SetProperty(p => p.RackId, "")         // 清除貨架
                        .SetProperty(p => p.PutTime, ""));      // 清除放置時間

                return Ok(new { message = "清除成功", stationNo = stationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "清除失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 標記空板 - 將站點從料盤(3)改為空板(1)，清除工單資訊
        /// 適用於 O/P/S/N 區
        /// </summary>
        [HttpPost]
        public IActionResult MarkEmptyTray([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查站點狀態是否為料盤
                if (port.HaveFlag != "3")
                {
                    return BadRequest(new { message = "站點狀態不是料盤，無法標記為空板" });
                }

                // 更新 oPort 表 - 標記為空板
                _DBContext.oPort
                    .Where(p => p.StationNo == stationNo)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.HaveFlag, "1")      // 設為空板
                        .SetProperty(p => p.WorkOrder, "")      // 清除工單
                        .SetProperty(p => p.RackId, ""));       // 清除貨架

                return Ok(new { message = "標記成功", stationNo = stationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "標記失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// Release - 將空板回送到指定區域
        /// O/P/S/N 區 → M 區（雷雕區）→ Q 區（出貨區）→ R 區
        /// EE 區（電梯暫存區）→ J 區（3F 插針室）
        /// </summary>
        [HttpPost]
        public IActionResult Release([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string stationNo = data.ContainsKey("stationNo") ? data["stationNo"] : "";

                if (string.IsNullOrEmpty(stationNo))
                {
                    return BadRequest(new { message = "請選擇站點" });
                }

                // 檢查站點是否存在
                var port = _DBContext.oPort.FirstOrDefault(p => p.StationNo == stationNo);
                if (port == null)
                {
                    return BadRequest(new { message = "站點不存在" });
                }

                // 檢查站點狀態是否為空板
                if (port.HaveFlag != "1")
                {
                    return BadRequest(new { message = "請先標記為空板" });
                }

                // 檢查是否已有待處理的派送任務（防止重複派送）
                var existingTask = _DBContext.oNeed
                    .FirstOrDefault(n => n.ObjStation == stationNo && 
                                         (n.AssignFlag == null || n.AssignFlag == ""));
                if (existingTask != null)
                {
                    return BadRequest(new { message = "此站點已有待處理的派送任務，終點：" + existingTask.EndStation });
                }

                // 取得已有待處理任務的終點站，避免重複指派到同一位置
                var pendingEndStations = _DBContext.oNeed
                    .Where(n => n.AssignFlag == null || n.AssignFlag == "")
                    .Select(n => n.EndStation)
                    .ToList();

                // 判斷起點區域，決定回送目的地
                var stationArea = stationNo.Substring(0, 1).ToUpper();
                oPort emptySlot = null;

                if (stationArea == "E")
                {
                    // EE 區（電梯暫存區）→ 回送到 J 區（3F 插針室）
                    emptySlot = _DBContext.oPort
                        .Where(p => p.Block == "J" && 
                                    p.HaveFlag == "0" && 
                                    (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                    p.UseFlag == "Y" &&
                                    !pendingEndStations.Contains(p.StationNo))
                        .OrderByDescending(p => p.Priority)
                        .ThenBy(p => p.Port)
                        .FirstOrDefault();

                    if (emptySlot == null)
                    {
                        return BadRequest(new { message = "3F 插針室 (J區) 沒有可放置的空位" });
                    }
                }
                else
                {
                    // O/P/S/N 區 → 依序尋找：M 區（雷雕區）→ Q 區（出貨區）→ R 區
                    emptySlot = _DBContext.oPort
                        .Where(p => p.Block == "M" && 
                                    p.HaveFlag == "0" && 
                                    (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                    p.UseFlag == "Y" &&
                                    !pendingEndStations.Contains(p.StationNo))
                        .OrderBy(p => p.Port)
                        .FirstOrDefault();

                    if (emptySlot == null)
                    {
                        // M 區滿，查詢 Q 區（出貨區）
                        emptySlot = _DBContext.oPort
                            .Where(p => p.Block == "Q" && 
                                        p.HaveFlag == "0" && 
                                        (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                        p.UseFlag == "Y" &&
                                        !pendingEndStations.Contains(p.StationNo))
                            .OrderBy(p => p.Port)
                            .FirstOrDefault();
                    }

                    if (emptySlot == null)
                    {
                        // Q 區滿，查詢 R 區
                        emptySlot = _DBContext.oPort
                            .Where(p => p.Block == "R" && 
                                        p.HaveFlag == "0" && 
                                        (p.BgnToEnd == null || p.BgnToEnd == "") &&
                                        p.UseFlag == "Y" &&
                                        !pendingEndStations.Contains(p.StationNo))
                            .OrderBy(p => p.Port)
                            .FirstOrDefault();
                    }

                    if (emptySlot == null)
                    {
                        return BadRequest(new { message = "目前沒有可放置的貨架" });
                    }
                }

                // 建立派送任務 (oNeed)
                string sql = "INSERT INTO oNeed (ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime, AssignFlag) VALUES({0},{1},{2},{3},{4},{5},{6})";
                _DBContext.Database.ExecuteSqlRaw(sql,
                    stationNo,                                    // ObjStation (起點)
                    "",                                           // RackId (空)
                    "",                                           // WorkOrder (空)
                    emptySlot.StationNo,                          // EndStation (終點)
                    "Web",                                        // TaskSource
                    DateTime.Now.ToString("yyyyMMddHHmmssffffff"), // TaskDateTime
                    "");                                          // AssignFlag

                return Ok(new { message = "Release 成功", stationNo = stationNo, endStation = emptySlot.StationNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Release 失敗", error = ex.Message });
            }
        }
    }

}
