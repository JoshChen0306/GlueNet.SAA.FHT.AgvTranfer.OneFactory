using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Serilog;
using SCP.Models;

namespace SCP.Controllers
{
    [Authorize (Roles ="1")]
    public class ProgramGroupController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public ProgramGroupController(ILogger<HomeController> logger, agvDB_1400004Context DBContext)
        {
            _logger = logger;
            _DBContext = DBContext;
        }
        
        public IActionResult Index(pGroup group)
        {
            ViewBag.GroupList = _DBContext.pGroup.OrderBy(g => g.GroupId).Select(g => new SelectListItem { Value = g.GroupId, Text = g.GroupId + "." + g.GroupCName });
            return View();
        }

        public IActionResult ShowpFunction(string groupId)
        {
            string[] sFunctionGroup = _DBContext.pGroup.Where(p => p.GroupId == groupId).Select(p => p.FunctionGroup).FirstOrDefault()?.Split(',');

            if (sFunctionGroup.IsNullOrEmpty())
                sFunctionGroup = new string[0];

            var query = (from p in _DBContext.pFunction
                         where p.FunctionNo != 0 && p.RowNo != 0
                         select new
                         {
                             Checkyn = (sFunctionGroup).Contains(p.FunctionNo.ToString()) ? "Y" : "N",
                             FunctionNo = p.FunctionNo,
                             FunctionType = p.FunctionType,
                             FunctionChineseName = p.FunctionChineseName,
                             FunctionEnglishName = p.FunctionEnglishName,
                             WebUrl = p.WebUrl
                         })
                        .OrderBy(p => p.FunctionNo);

            ViewBag.Function = query;
            return PartialView("_pFunctionPartialView");
        }

        [HttpPost]
        public IActionResult Save([FromBody] Dictionary<string, List<string>> updatedata)
        {
            if (updatedata.Count > 0)
            {
                string sGroupId = updatedata.Keys.FirstOrDefault();
                List<string> lsFunctionNo = updatedata.Values.ToList()[0];
                UpdateGroup(sGroupId, lsFunctionNo);
            }
            return Ok();
        }

        private void UpdateGroup(string _sGroupId, List<string> _lsFunctionNo)
        {
            var currentTime = DateTime.Now.ToString("yyyyMMddHHmmss");

            try
            {
                _DBContext.pGroup
                   .Where(g => g.GroupId == _sGroupId)
                   .ExecuteUpdate(setters => setters
                       .SetProperty(g => g.FunctionGroup, string.Join(",", _lsFunctionNo))
                       .SetProperty(g => g.ModifiedTime, currentTime));
            }
            catch (Exception ex)
            {
            }

        }
    }
}