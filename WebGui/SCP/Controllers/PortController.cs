using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize(Roles = "1")]
    public class PortController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public PortController(IConfiguration configuration, agvDB_1400004Context DBContext)
        {
            _configuration = configuration;
            _DBContext = DBContext;
        }
        public IActionResult Index(string floor = "1F")
        {
            // 取得使用者允許的樓層（如 ["FHT1-1F", "FHT1-3F"]）
            var allowedFloors = GetUserAllowedFloors();

            // floor 參數容錯：若指定樓層不在允許範圍，退回第一個允許樓層
            if (allowedFloors.Any() && !allowedFloors.Contains($"FHT1-{floor}"))
            {
                floor = allowedFloors.First().Replace("FHT1-", "");
            }

            // 讀取樓層顯示名稱（來自 appsettings.json FloorSettings）
            var floorDisplayNames = _configuration.GetSection("FloorSettings")
                .GetChildren()
                .ToDictionary(
                    x => x.Key,
                    x => x.GetSection("DisplayName").Value ?? x.Key
                );

            ViewBag.AllowedFloors = allowedFloors;
            ViewBag.FloorDisplayNames = floorDisplayNames;

            // 將 floor 轉換為 area 格式 (如 "1F" -> "FHT1-1F")
            string area = $"FHT1-{floor}";

            // 樓層與區域對應（從 appsettings.json FloorSettings 讀取，與權限篩選同一來源）
            var blocks = _configuration.GetSection($"FloorSettings:{area}:Areas").Get<string[]>()
                ?? _configuration.GetSection("FloorSettings:FHT1-1F:Areas").Get<string[]>()
                ?? Array.Empty<string>();

            #region [讀取暫存架位置及狀態]
            List<oPort> query = _DBContext.oPort
                .Where(p => blocks.Contains(p.Block))
                .ToList();
            List<Position> result = new List<Position>();

            foreach (var item in query)
            {
                // 跳過沒有座標設定的站點
                if (string.IsNullOrEmpty(item.Remark) || !item.Remark.Contains(","))
                    continue;

                Position data = new Position
                {
                    Name = item.StationNo,
                    Left = ConvertX(item.Remark.Split(",")[0], area),
                    Bottom = ConvertY(item.Remark.Split(",")[1], area),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = GetStationImgSrc(item.HaveFlag, item.WorkOrder),
                    Reserve = string.IsNullOrEmpty(item.BgnToEnd) ? "N" : "Y",
                    InterfaceName = item.InterfaceName,
                    MachineName = item.MachineName,
                    UseFlag = item.UseFlag,
                    HaveFlag = item.HaveFlag,
                    WorkOrder = item.WorkOrder,
                };
                result.Add(data);
            }
            #endregion

            ViewBag.positions = result;
            ViewBag.CurrentFloor = floor;
            ViewBag.MapImage = $"/img/FHT1-{floor}.png";
            return View();
        }

        /// <summary>
        /// 取得使用者允許的樓層清單（根據 AgvSetting 和路線權限）
        /// </summary>
        /// <remarks>
        /// TODO: 此邏輯與 CommonController.GetUserAllowedFloors 重複，未來第三個頁面需要時應抽成共用 FloorService。
        /// </remarks>
        private List<string> GetUserAllowedFloors()
        {
            // 1. 從 AgvSetting 讀取系統可用樓層（有座標設定的樓層）
            var agvSettings = _configuration.GetSection("AgvSetting").GetChildren();
            var allFloors = agvSettings.Select(s => s.Key).ToList();

            if (!allFloors.Any())
            {
                return new List<string>();
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                // 未登入使用者，顯示所有樓層
                return allFloors;
            }

            // 2. 取得使用者的路線權限
            var userRoutes = _DBContext.pUserRoute
                .Where(ur => ur.UserId == userId)
                .Join(_DBContext.pRoute.Where(r => r.ControlFlag == "Y"),
                      ur => ur.RouteId,
                      r => r.RouteId,
                      (ur, r) => r)
                .ToList();

            if (!userRoutes.Any())
            {
                // 使用者沒有設定路線權限，顯示所有樓層
                return allFloors;
            }

            // 3. 取得使用者所有允許的起點區域
            var allowedAreas = userRoutes
                .Where(r => !string.IsNullOrEmpty(r.SourceAreas))
                .SelectMany(r => r.SourceAreas.Split(',').Select(a => a.Trim()))
                .Distinct()
                .ToList();

            // 4. 讀取 FloorSettings 配置，取得每個樓層包含的區域
            var floorSettings = _configuration.GetSection("FloorSettings")
                .GetChildren()
                .ToDictionary(
                    x => x.Key,
                    x => x.GetSection("Areas").Get<string[]>() ?? Array.Empty<string>()
                );

            // 5. 篩選使用者可見的樓層（該樓層的區域與使用者允許區域有交集）
            var allowedFloors = allFloors
                .Where(f =>
                {
                    if (!floorSettings.ContainsKey(f)) return true; // 無配置則顯示
                    return floorSettings[f].Any(a => allowedAreas.Contains(a));
                })
                .ToList();

            return allowedFloors.Any() ? allowedFloors : allFloors;
        }

        public IActionResult UpdateoPort([FromBody] Dictionary<string, string> port)
        {
            //Dictionary<string, string> portDict = port.ToDictionary(item => item["name"], item => item["value"]);
            string name = port["name"];
            string machinename = port["machinename"];
            string interfacename = port["interfacename"];
            string haveflag = port["haveflag"];
            string workorder = (haveflag == "3") ? port["workorder"] : "";
            string useflag = port.ContainsKey("useflag") ? "Y" : "N";

            try
            {
                _DBContext.oPort
                    .Where(p => p.StationNo == name)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(p => p.MachineName, machinename)
                        .SetProperty(p => p.InterfaceName, interfacename)
                        .SetProperty(p => p.HaveFlag, haveflag)
                        .SetProperty(p => p.WorkOrder, workorder)
                        .SetProperty(p => p.UseFlag, useflag));
            }
            catch (Exception ex)
            {

            }

            return Ok();
        }

        /// <summary>
        /// 根據 HaveFlag 和 WorkOrder 決定站點圖示
        /// </summary>
        private string GetStationImgSrc(string haveFlag, string workOrder)
        {
            // 處理 null 或空值的情況，預設為空架 (0)
            if (string.IsNullOrEmpty(haveFlag))
            {
                haveFlag = "0";
            }

            // V Cut 物料顏色判斷：只有 HaveFlag=3 且有 WorkOrder 時才檢查
            if (haveFlag == "3" && !string.IsNullOrEmpty(workOrder))
            {
                if (workOrder.Contains("^VCUT^DONE"))
                {
                    // 紫色：V Cut 已加工完成
                    return "/img/vcut-done.svg";
                }
                else if (workOrder.Contains("^VCUT"))
                {
                    // 橙色：V Cut 待加工
                    return "/img/vcut-pending.svg";
                }
            }
            // 預設：使用設定檔的圖示
            var imgSrc = _configuration.GetSection("TracStatus").GetSection(haveFlag).Value;
            // 如果設定檔中找不到對應的圖示，使用空架圖示作為預設
            return imgSrc ?? "/img/empty.svg";
        }

        private string ConvertX(string posX, string area)
        {
            string result;
            var setting = _configuration.GetSection($"AgvSetting:{area}");
            double minPercentX = Convert.ToDouble(setting["minPercentX"]);
            double maxPercentX = Convert.ToDouble(setting["maxPercentX"]);
            double minX = Convert.ToDouble(setting["minX"]);
            double maxX = Convert.ToDouble(setting["maxX"]);
            double percentRangeX = maxPercentX - minPercentX;
            double rangeX = maxX - minX;

            double normalizedX = (Convert.ToDouble(posX) - minX) / rangeX;
            // 將0-1範圍的X座標轉換為minPercent-maxPercent%範圍
            result = (minPercentX + (normalizedX * percentRangeX)).ToString() + "%";
            return result;
        }

        private string ConvertY(string posY, string area)
        {
            string result;
            var setting = _configuration.GetSection($"AgvSetting:{area}");
            double minPercentY = Convert.ToDouble(setting["minPercentY"]);
            double maxPercentY = Convert.ToDouble(setting["maxPercentY"]);
            double minY = Convert.ToDouble(setting["minY"]);
            double maxY = Convert.ToDouble(setting["maxY"]);
            double percentRangeY = maxPercentY - minPercentY;
            double rangeY = maxY - minY;

            double normalizedY = (Convert.ToDouble(posY) - minY) / rangeY;
            // 將0-1範圍的Y座標轉換為minPercent-maxPercent%範圍
            result = (minPercentY + (normalizedY * percentRangeY)).ToString() + "%";
            return result;
        }
    }
}
