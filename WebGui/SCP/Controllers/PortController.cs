using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize(Roles ="1")]
    public class PortController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly agvDB_1400004Context _DBContext;
        public PortController(IConfiguration configuration,agvDB_1400004Context DBContext)
        {
            _configuration = configuration;
            _DBContext = DBContext;
        }
        public IActionResult Index(string floor = "1F")
        {
            // 樓層與區域對應
            var floorBlocks = new Dictionary<string, string[]>
            {
                { "1F", new[] { "A", "B", "C", "D", "E", "F", "EE" } },
                { "2F", new[] { "H", "M", "N", "O", "P", "Q", "R", "S", "T" } },
                { "3F", new[] { "J", "I" } },
                { "4F", new[] { "K", "L" } }
            };
            
            var blocks = floorBlocks.ContainsKey(floor) ? floorBlocks[floor] : floorBlocks["1F"];
            
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
                    Left = ConvertX(item.Remark.Split(",")[0]),
                    Bottom = ConvertY(item.Remark.Split(",")[1]),
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
            ViewBag.MapImage = $"/img/FHT2-{floor}.png";
            return View();
        }
        public IActionResult UpdateoPort([FromBody] Dictionary<string, string> port) 
        {
            //Dictionary<string, string> portDict = port.ToDictionary(item => item["name"], item => item["value"]);
            string name = port["name"];
            string machinename = port["machinename"];
            string interfacename = port["interfacename"];
            string haveflag = port["haveflag"];
            string workorder = (haveflag=="3")?port["workorder"]:"";
            string useflag = port.ContainsKey("useflag")?"Y":"N";

            try
            {
                _DBContext.oPort
                    .Where(p =>p.StationNo == name)
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

        private string ConvertX(string posX)
        {
            string result;
            double minPercentX = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("minPercentX").Value);
            double maxPercentX = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("maxPercentX").Value);
            double minX = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("minX").Value);
            double maxX = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("maxX").Value);
            double percentRangeX = maxPercentX - minPercentX;
            double rangeX = maxX - minX;

            double normalizedX = (Convert.ToDouble(posX) - minX) / rangeX;
            // 將0-1範圍的X座標轉換為minPercent-maxPercent%範圍
            result = (minPercentX + (normalizedX * percentRangeX)).ToString() + "%";
            return result;
        }

        private String ConvertY(string posY)
        {
            string result;
            double minPercentY = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("minPercentY").Value);
            double maxPercentY = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("maxPercentY").Value);
            double minY = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("minY").Value);
            double maxY = Convert.ToDouble(_configuration.GetSection("AgvSetting").GetSection("maxY").Value);
            double percentRangeY = maxPercentY - minPercentY;
            double rangeY = maxY - minY;

            double normalizedY = (Convert.ToDouble(posY) - minY) / rangeY;
            // 將0-1範圍的X座標轉換為minPercent-maxPercent%範圍
            result = (minPercentY + (normalizedY * percentRangeY)).ToString() + "%";
            return result;
        }
    }
}
