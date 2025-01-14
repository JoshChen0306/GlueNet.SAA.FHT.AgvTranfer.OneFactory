using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCP.Models;
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
        public IActionResult ShowMap()
        {

            ViewBag.positions = GetTrac();
            ViewBag.AgvPositions = GetAgv();

            return PartialView("_MapPartial");
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
                    Left = ConvertX(item.Remark.Split(",")[0]),
                    Bottom = ConvertY(item.Remark.Split(",")[1]),
                    Transform = "rotate(" + item.Remark.Split(",")[2] + "deg)",
                    ImgSrc = _configuration.GetSection("TracStatus").GetSection(item.HaveFlag).Value,
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

        private List<oShuttle> GetAgv()
        {
            #region [讀取車輛狀態及位置]
            List<oShuttle> AgvPositions = _DBContext.oShuttle.ToList();
            foreach (var item in AgvPositions)
            {
                item.PosX = ConvertX(item.PosX);
                item.PosY = ConvertY(item.PosY);
            }
            #endregion
            return AgvPositions;
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
