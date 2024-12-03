using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SCP.Models;
using System;
using System.Globalization;

namespace SCP.Controllers
{
    public class ActivationController : Controller
    {
        private readonly agvDB_1400004Context _DBContext;

        public ActivationController(agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GetActivation(string startDate, string endDate, string shuttleId)
        {
            double totalHours = GetTotalHours(startDate, endDate);
            var data = GetSearchData(startDate, endDate, shuttleId);
            var result = GetPieActivation(data, totalHours);

            return Json(result);
        }

        public IActionResult GetTaskTable(string startDate, string endDate, string shuttleId)
        {
            var data = GetSearchData(startDate, endDate, shuttleId);
            double totalHours = GetTotalHours(startDate, endDate);

            ViewBag.Activation = GetPieActivation(data, totalHours);
            ViewBag.TotalActivation = data
               .Select(item => new
               {
                   item.TaskType,
                   BeginTime = DateTime.ParseExact(item.BeginTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
                   EndTime = DateTime.ParseExact(item.EndTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
               })
               .GroupBy(item => new { item.TaskType })
               .Select(group =>
               
                   Math.Round(Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2) / (totalHours) * 100, 2)
               ).FirstOrDefault().ToString("0.00");
            return PartialView("_ActivationPartial");
        }

        private List<ubActivation> GetSearchData(string startDate, string endDate, string shuttleId)
        {
            (string beginTime, string endTime) = GetShiftTime(startDate,endDate);

            var data = _DBContext.ubActivation
                .Where(item => item.BeginTime.CompareTo(beginTime) >= 0
                && item.BeginTime.CompareTo(endTime) <= 0
                && (string.IsNullOrEmpty(shuttleId) || item.ShuttleId == shuttleId)
                && !string.IsNullOrEmpty(item.EndTime))
                .ToList();


            return data;
        }

        private List<ActivationReport> GetPieActivation(List<ubActivation> data, double totalHours)
        {
            //double totalHours = GetTotalHours(startDate,endDate);

            var result = data
               .Select(item => new
               {
                   item.ShuttleId,
                   item.TaskType,
                   BeginTime = DateTime.ParseExact(item.BeginTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
                   EndTime = DateTime.ParseExact(item.EndTime, "yyyyMMddHHmmssffffff", CultureInfo.InvariantCulture),
               })
               .GroupBy(item => new { item.ShuttleId })
               .Select(group => new ActivationReport
               {
                   ShuttleId = group.Key.ShuttleId,
                   Travling = Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Idle = Math.Round(totalHours - group.Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Charging = Math.Round(group.Where(item => item.TaskType == "C").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Alarm = Math.Round(group.Where(item => item.TaskType == "A").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Offline = Math.Round(group.Where(item => item.TaskType == "F").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   NonTraveling = Math.Round(totalHours - group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2).ToString("0.00"),
                   Activation = Math.Round(Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 2)
                   / (totalHours) * 100, 2).ToString("0.00")
               })               
               .OrderBy(item => item.ShuttleId)
               .ToList();
            
            return result;
        }

        private (string, string) GetShiftTime(string startDate , string endDate)
        {
            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            string beginTime = string.Empty;
            string endTime = string.Empty;
            string shiftTime = _DBContext.pShift.Where(item => item.ShiftName == "早班").Select(item => item.BeginDateTime).FirstOrDefault();

            if (DateTime.Now.TimeOfDay < TimeSpan.Parse(shiftTime))
            {
                beginTime = dtStart.AddDays(-1).ToString("yyyyMMdd") + shiftTime.Replace(":", "");
                endTime = dtEnd.ToString("yyyyMMdd") + shiftTime.Replace(":", "");
            }
            else
            {
                beginTime = dtStart.ToString("yyyyMMdd") + shiftTime.Replace(":", "");
                endTime = dtEnd.AddDays(+1).ToString("yyyyMMdd") + shiftTime.Replace(":", "");
            }


            return (beginTime, endTime);
        }
        
        private double GetTotalHours(string startDate, string endDate)
        {
            double totalHours;
            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture).AddHours(8).AddMinutes(30);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            if (dtEnd == DateTime.Now.Date)
            {
                if (DateTime.Now < dtStart)
                {
                    totalHours = Math.Round((DateTime.Now - dtStart.AddDays(-1)).TotalHours, 2);
                }
                else
                {
                    totalHours = Math.Round((DateTime.Now - dtStart).TotalHours, 2);
                }

            }
            else
            {
                totalHours = Math.Round((dtEnd.AddDays(1).AddHours(8).AddMinutes(30) - dtStart).TotalHours, 2);
            }

            return totalHours;
        }
    }
}
