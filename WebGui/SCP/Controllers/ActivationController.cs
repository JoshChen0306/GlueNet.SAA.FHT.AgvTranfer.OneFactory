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
            var data = GetSearchData(startDate, endDate, shuttleId);
            var result = GetPieActivation(data, startDate, endDate);

            return Json(result);
        }

        public IActionResult GetTaskTable(string startDate, string endDate, string shuttleId)
        {
            var data = GetSearchData(startDate, endDate, shuttleId);
            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture);

            ViewBag.Activation = GetPieActivation(data, startDate, endDate);
            //ViewBag.StartDate = dtStart.ToString("M/d");
            //ViewBag.EndDate = dtEnd.ToString("M/d");

            return PartialView("_ActivationPartial");
        }

        private List<ubActivation> GetSearchData(string startDate, string endDate, string shuttleId)
        {
            var data = _DBContext.ubActivation
                .Where(item => item.BeginTime.Substring(0, 8).CompareTo(startDate) >= 0
                && item.BeginTime.Substring(0, 8).CompareTo(endDate) <= 0
                && (string.IsNullOrEmpty(shuttleId) || item.ShuttleId == shuttleId)
                && !string.IsNullOrEmpty(item.EndTime))
                .ToList();

            return data;
        }

        private List<ActivationReport> GetPieActivation(List<ubActivation> data, string startDate, string endDate)
        {

            DateTime dtStart = DateTime.ParseExact(startDate, "yyyyMMdd", CultureInfo.InvariantCulture);
            DateTime dtEnd = DateTime.ParseExact(endDate, "yyyyMMdd", CultureInfo.InvariantCulture);

            double totalHours = Math.Round((dtEnd.AddDays(1) - dtStart).TotalHours, 1);

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
                   Travling = Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 1),
                   Idle = Math.Round(totalHours - group.Sum(item => (item.EndTime - item.BeginTime).TotalHours), 1),
                   Charging = Math.Round(group.Where(item => item.TaskType == "C").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 1),
                   Alarm = Math.Round(group.Where(item => item.TaskType == "A").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 1),
                   Offline = Math.Round(group.Where(item => item.TaskType == "F").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 1),
                   NonTraveling = Math.Round(totalHours - group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours), 1),
                   Activation = Math.Round(group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours)
                   / (totalHours - group.Where(item => item.TaskType == "R").Sum(item => (item.EndTime - item.BeginTime).TotalHours)) * 100, 1)
               })
               .OrderBy(item => item.ShuttleId)
               .ToList();

            return result;
        }
    }
}
