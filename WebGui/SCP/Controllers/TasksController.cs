using Microsoft.AspNetCore.Mvc;
using SCP.Models;
using System.Globalization;
using System.Reflection;

namespace SCP.Controllers
{
    public class TasksController : Controller
    {
        private readonly agvDB_1400004Context _DBContext;
        public TasksController(agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GetMission(string startDate, string endDate, int shuttleId, string shiftId)
        {
           
            var missions = GetSearchData(startDate, endDate, shuttleId, shiftId);
            ViewBag.Missions = missions
                .GroupBy(item => item.Date)
                .Select(group => new 
                {
                    Date = group.Key.ToString("M/d"),
                    ShuttleName = group.First().AGV,
                    DayShift = group.Where(i => i.ShiftName == "早班").Count().ToString(),
                    NightShift = group.Where(i => i.ShiftName == "晚班").Count().ToString(),
                    Total = group.Count().ToString()
                })
                .OrderBy(item => item.Date);

            return PartialView("_TaskDataPartial");
        }

        public IActionResult GetBarChat(string startDate, string endDate, int shuttleId, string shiftId)
        {
     
            var missions = GetSearchData(startDate, endDate, shuttleId, shiftId); 
            var results = missions
                .GroupBy(item => new {item.Date ,item.ShiftName })
                .Select(group => new
                {
                    Date = group.Key.Date,
                    ShiftName = group.Key.ShiftName,
                    Count = group.Count()
                })
                .OrderBy(item => item.Date);

            return Json(results);
        }

        public IActionResult GetTasks(string startDate, string endDate, int shuttleId, string shiftId)
        {
            var missions = GetSearchData(startDate, endDate, shuttleId, shiftId);
            ViewBag.Tasks = missions         
                .Select(item => new 
                {
                    Date = item.Date.ToString("M/d"),
                    AGV = item.AGV,
                    ShiftName = item.ShiftName,
                    PartNo = item.PartNo,
                    BeginStation = item.BeginStation,
                    EndStation = item.EndStation,
                    BeginTime = item.BeginTime,
                    EndTime = item.EndTime,
                    TotalTime = item.TotalTime
                })
                .OrderBy(item => item.Date);

            return PartialView("_TaskDetialPartial");
        }

        private List<TaskReport> GetSearchData(string startDate, string endDate, int shuttleId ,string shiftId)
        {
            

            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture).AddDays(1);
            List<TaskReport> missionData = new List<TaskReport>();

            string shuttleName = shuttleId == 0 ? "所有車輛" : _DBContext.oShuttle.Where(s => s.ShuttleId == shuttleId).FirstOrDefault().GustomerName;

            var missions = _DBContext.ubMission
                .Where(item => item.BeginTime.Substring(0, 8).CompareTo(startDate) >= 0
                && item.BeginTime.Substring(0, 8).CompareTo(endDate) <= 0
                && (shuttleId == 0 || item.ShuttleId == shuttleId))
                .ToList();

            var shifts = _DBContext.pShift
                .ToList()// 將查詢結果拉取到內存中，以便進行後續的分組和排序操作
                .Where(s => DateTime.ParseExact(s.EffectDateTime, "yyyy-MM-dd", CultureInfo.InvariantCulture) < dtEnd
                && (s.ShiftCode == shiftId || string.IsNullOrEmpty(shiftId))) // 篩選出今天日期大於記錄中日期的值
                .Select(s => new
                {
                    s.ShiftName,
                    BeginDateTime = DateTime.ParseExact(s.BeginDateTime, "HH:mm", CultureInfo.InvariantCulture),
                    EndDateTime = DateTime.ParseExact(s.EndDateTime, "HH:mm", CultureInfo.InvariantCulture),
                    s.EffectDateTime
                })
                .GroupBy(s => s.ShiftName) // 按照班次類型進行分組
                .Select(g => g.OrderByDescending(s => s.EffectDateTime) // 在每個分組內按日期降序排列
                .First()) // 從每組中取出第一筆記錄
                .ToList();

            var data = missions
               .Select(m => new
               {
                   Date = DateTime.ParseExact(m.EndTime.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture),
                   PartNo = m.WorkOrder,
                   BeginStation = m.BeginStation,
                   EndStation = m.EndStation,
                   BeginTime = DateTime.ParseExact(m.BeginTime.Substring(8, 4), "HHmm", CultureInfo.InvariantCulture),
                   EndTime = DateTime.ParseExact(m.EndTime.Substring(8, 4), "HHmm", CultureInfo.InvariantCulture),
               })
               .ToList();

            foreach (var shiftTime in shifts)
            {
                var result = data
                    .Where(m => shiftTime.BeginDateTime > shiftTime.EndDateTime
                                ? m.EndTime >= shiftTime.BeginDateTime || m.EndTime < shiftTime.EndDateTime
                                : m.EndTime >= shiftTime.BeginDateTime && m.EndTime < shiftTime.EndDateTime)
                    .Select(m => new TaskReport
                    {
                        Date = shiftTime.BeginDateTime > shiftTime.EndDateTime && m.EndTime < shiftTime.EndDateTime ? m.Date.AddDays(-1) : m.Date,
                        AGV = shuttleName,
                        ShiftName = shiftTime.ShiftName,
                        PartNo = m.PartNo,
                        BeginStation = m.BeginStation,
                        EndStation = m.EndStation,
                        BeginTime = m.BeginTime.ToString("HH:mm"),
                        EndTime = m.EndTime.ToString("HH:mm"),
                        TotalTime = m.BeginTime > m.EndTime ? (m.EndTime.AddDays(1) - m.BeginTime).TotalMinutes.ToString() : (m.EndTime - m.BeginTime).TotalMinutes.ToString()
                    })
                    .Where(m => m.Date >= dtStart && m.Date < dtEnd)
                    .OrderBy(m => m.Date)
                    .ToList();
                missionData.AddRange(result);
            }

            return missionData;
        }
    }
}
