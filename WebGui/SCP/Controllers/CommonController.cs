using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCP.Models;
using SCP.Helpers;
using System;
using System.Globalization;
using System.Security.Cryptography.Xml;

namespace SCP.Controllers
{
    [Route("api/[controller]")]
    public class CommonController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public CommonController(IConfiguration configuration, agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("ShowMap")]
        public IActionResult ShowMap(string area = "")
        {
            try
            {
                // 根據使用者路線權限過濾可見樓層
                var allowedFloors = GetUserAllowedFloors();
                ViewBag.AllowedFloors = allowedFloors;

                // 如果未指定 area 或指定的 area 不在允許的樓層中，使用第一個允許的樓層
                if (string.IsNullOrEmpty(area) || !allowedFloors.Contains(area))
                {
                    area = allowedFloors.FirstOrDefault() ?? "FHT1-1F";
                }

                LogMgt.Logger?.Info($"[ShowMap] 開始載入地圖, area={area}");
                
                LogMgt.Logger?.Debug($"[ShowMap] 正在取得站點資料...");
                var positions = GetTrac(area);
                LogMgt.Logger?.Info($"[ShowMap] 站點資料載入成功, 共 {positions.Count} 個站點");
                ViewBag.positions = positions;
                
                LogMgt.Logger?.Debug($"[ShowMap] 正在取得 AGV 資料...");
                var agvPositions = GetAgv(area);
                LogMgt.Logger?.Info($"[ShowMap] AGV 資料載入成功, 共 {agvPositions.Count} 個 AGV");
                ViewBag.AgvPositions = agvPositions;
                ViewBag.CurrentArea = area;

                // 傳遞樓層顯示名稱給前端
                var floorDisplayNames = _configuration.GetSection("FloorSettings")
                    .GetChildren()
                    .ToDictionary(
                        x => x.Key,
                        x => x.GetSection("DisplayName").Value ?? x.Key
                    );
                ViewBag.FloorDisplayNames = floorDisplayNames;

                LogMgt.Logger?.Info($"[ShowMap] 地圖載入完成");
                return PartialView("_MapPartial");
            }
            catch (Exception ex)
            {
                LogMgt.Logger?.Error(ex, $"[ShowMap] 錯誤: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 取得使用者允許的樓層清單（根據 AgvSetting 和路線權限）
        /// </summary>
        private List<string> GetUserAllowedFloors()
        {
            // 1. 從 AgvSetting 讀取系統可用樓層（有座標設定的樓層）
            var agvSettings = _configuration.GetSection("AgvSetting").GetChildren();
            var allFloors = agvSettings.Select(s => s.Key).ToList();
            
            if (!allFloors.Any())
            {
                LogMgt.Logger?.Warn("[GetUserAllowedFloors] AgvSetting 無任何樓層配置");
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
                .Where(floor => {
                    if (!floorSettings.ContainsKey(floor)) return true; // 無配置則顯示
                    return floorSettings[floor].Any(a => allowedAreas.Contains(a));
                })
                .ToList();

            return allowedFloors.Any() ? allowedFloors : allFloors;
        }

        [HttpGet("UpdateTrac")]
        public IActionResult UpdateTrac()
        {
            var data = Json(GetTrac());
            return data;
        }
        [HttpGet("UpdateAgv")]
        public IActionResult UpdateAgv()
        {
            var data = Json(GetAgv());
            return data;
        }

        [HttpGet("GetHitchhikeStation")]
        public Dictionary<string, string>? GetHitchhikeStation()
        {
            var mission = _DBContext.oMission.FirstOrDefault(x => x.EndStation.StartsWith("G") && x.OkFlag == "Y");
            Dictionary<string, string>? station = new Dictionary<string, string>();
            if (mission != null)
            {
                var beginStation = _DBContext.oPort.FirstOrDefault(x => x.Block == "G" && x.HaveFlag == "1");
                var endStation = _DBContext.oPort.FirstOrDefault(x => x.Block == "J" && x.HaveFlag == "0");
                if (beginStation != null && endStation != null)
                {
                    station.Add("beginStation", beginStation.StationNo);
                    station.Add("endStation", endStation.StationNo);
                }
                else station = null;
            }
            return station;
        }

        private List<Position> GetTrac(string area)
        {
            #region [讀取暫存架位置及狀態]      
            List<oPort> query = _DBContext.oPort.Where(p => p.UseFlag == "Y" && p.Area == area).ToList();
            List<Position> result = new List<Position>();

            // 檢查是否需要交換 XY 軸
            var areaSetting = _configuration.GetSection($"AgvSetting:{area}");
            bool swapXY = areaSetting["swapXY"] == "true";

            foreach (var item in query)
            {
                string posX = item.Remark.Split(",")[0];
                string posY = item.Remark.Split(",")[1];
                
                // 如果 swapXY 為 true，交換 X 和 Y 座標
                if (swapXY)
                {
                    (posX, posY) = (posY, posX);
                }

                Position data = new Position
                {
                    Name = item.StationNo,
                    Left = ConvertX(posX, area),
                    Bottom = ConvertY(posY, area),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = GetStationImgSrc(item.HaveFlag, item.WorkOrder),
                    Reserve = string.IsNullOrEmpty(item.BgnToEnd) ? "N" : "Y",
                    HaveFlag = item.HaveFlag,
                    RackId = item.RackId,
                    WorkOrder = item.WorkOrder,
                    InterfaceName = item.InterfaceName,
                    PutTime = item.PutTime
                };
                result.Add(data);
            }
            #endregion
            return result;
        }
        private List<Position> GetTrac()
        {
            #region [讀取暫存架位置及狀態]      
            List<oPort> query = _DBContext.oPort.Where(p => p.UseFlag == "Y").ToList();
            List<Position> result = new List<Position>();

            foreach (var item in query)
            {
                Position data = new Position
                {
                    Name = item.StationNo,
                    Left = ConvertX(item.Remark.Split(",")[0], item.Area),
                    Bottom = ConvertY(item.Remark.Split(",")[1], item.Area),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = GetStationImgSrc(item.HaveFlag, item.WorkOrder),
                    Reserve = string.IsNullOrEmpty(item.BgnToEnd) ? "N" : "Y",
                    HaveFlag = item.HaveFlag,
                    RackId = item.RackId,
                    WorkOrder = item.WorkOrder,
                    InterfaceName = item.InterfaceName,
                    PutTime = item.PutTime
                };
                result.Add(data);
            }
            #endregion
            return result;
        }

        /// <summary>
        /// 將 Web 區域代碼轉換為海康 MapCode
        /// </summary>
        private string GetMapCodeFromArea(string area)
        {
            var mapping = _configuration.GetSection("MapCodeMapping").Get<Dictionary<string, string>>();
            if (mapping != null && mapping.ContainsKey(area))
            {
                return mapping[area];
            }
            // 如果找不到映射，返回原始 area（向後兼容）
            return area;
        }

        private List<oShuttle> GetAgv(string area)
        {
            #region [讀取車輛狀態及位置]
            // 將 Web 區域代碼轉換為海康 MapCode
            string mapCode = GetMapCodeFromArea(area);
            
            List<oShuttle> AgvPositions = _DBContext.oShuttle.Where(x => x.MapCode == mapCode).ToList();
            // 檢查是否需要交換 XY 軸
            var areaSetting = _configuration.GetSection($"AgvSetting:{area}");
            bool swapXY = areaSetting["swapXY"] == "true";

            foreach (var item in AgvPositions)
            {
                string posX = item.PosX;
                string posY = item.PosY;
                
                // 如果 swapXY 為 true，交換 X 和 Y 座標
                if (swapXY)
                {
                    (posX, posY) = (posY, posX);
                }

                // 使用原始 area 進行座標轉換（因為 appsettings 使用 FHT1-1F 作為 key）
                item.PosX = ConvertX(posX, area);
                item.PosY = ConvertY(posY, area);
            }
            #endregion
            return AgvPositions;
        }
        private List<oShuttle> GetAgv()
        {
            #region [讀取車輛狀態及位置]
            List<oShuttle> AgvPositions = _DBContext.oShuttle.ToList();
            var mapping = _configuration.GetSection("MapCodeMapping").Get<Dictionary<string, string>>();

            foreach (var item in AgvPositions)
            {
                // 反向尋找 Area Code (例如 "CC" -> "FHT1-3F")
                string area = mapping.FirstOrDefault(x => x.Value == item.MapCode).Key;

                // 如果找不到對應的區域，就使用 MapCode 當作預設 (雖然可能找不到設定)
                if (string.IsNullOrEmpty(area))
                {
                    area = item.MapCode;
                }

                // 讀取該區域的設定
                var setting = _configuration.GetSection($"AgvSetting:{area}");
                // 檢查是否需要交換 XY 軸
                bool swapXY = setting["swapXY"] == "true";

                string posX = item.PosX;
                string posY = item.PosY;

                // 如果 swapXY 為 true，交換 X 和 Y 座標
                if (swapXY)
                {
                    (posX, posY) = (posY, posX);
                }

                item.PosX = ConvertX(posX, area);
                item.PosY = ConvertY(posY, area);
            }
            #endregion
            return AgvPositions;
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

            // 特殊物料顏色判斷：只有 HaveFlag=3 且有 WorkOrder 時才檢查
            if (haveFlag == "3" && !string.IsNullOrEmpty(workOrder))
            {
                // 優先順序：^NG → ^RETURN → ^VCUT^DONE → ^VCUT
                if (workOrder.Contains("^NG"))
                {
                    // 橙色：NG 回送物料（品檢失敗）
                    return "/img/ng-material.svg";
                }
                else if (workOrder.Contains("^RETURN"))
                {
                    // 黃色：Release 回送空板（不可派送）
                    return "/img/return-material.svg";
                }
                else if (workOrder.Contains("^VCUT^DONE"))
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

        // 委派共用工具，邏輯集中於 MapCoordinateConverter（庫位/車/充電樁共用）
        private string ConvertX(string posX, string area)
            => MapCoordinateConverter.ConvertX(_configuration, area, posX);

        private string ConvertY(string posY, string area)
            => MapCoordinateConverter.ConvertY(_configuration, area, posY);
    }
}
