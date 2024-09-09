
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using SCP.Models;

namespace SCP.Controllers
{
    public class GroupController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public GroupController(ILogger<HomeController> logger, agvDB_1400004Context DBContext)
        {
            _logger = logger;
            _DBContext = DBContext;
        }

        public IActionResult Index()
        {
            //Get Group
            var query = _DBContext.pGroup;
            return View(query);
        }
        [HttpPost]
        public IActionResult DataChange([FromBody] DataChange data)
        {
            var insertdata = data.insertdata;
            var updatedata = data.updatedata;
            var deletedata = data.deletedata;

            if (insertdata.Count > 0)
            {
                foreach (var item in insertdata)
                {
                    pGroup? group = JsonConvert.DeserializeObject<pGroup>(item.Value.ToString());
                    InsertGroup(group);

                }
            }

            if (updatedata.Count > 0)
            {
                foreach (var item in updatedata)
                {
                    pGroup? group = JsonConvert.DeserializeObject<pGroup>(item.Value.ToString());
                    UpdateGroup(group);

                }
            }

            if (deletedata != null)
            {

                DeleteGroup(deletedata);
            }

            return Ok();
        }

        private void InsertGroup(pGroup group)
        {

            try
            {
                string sql = "INSERT INTO pGroup (GroupId, GroupCName, GroupEName, LogoutTime,ModifiedTime) VALUES ({0}, {1}, {2}, {3} ,{4})";
                _ = _DBContext.Database.ExecuteSqlRaw(sql, group.GroupId, group.GroupCName, group.GroupEName, group.LogoutTime, DateTime.Now.ToString("yyyyMMddHHmmss"));
            }
            catch (Exception ex)
            {

            }
        }

        private void UpdateGroup(pGroup group)
        {
            var currentTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            try
            {
                _DBContext.pGroup
                    .Where(g => g.GroupId == group.GroupId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(g => g.GroupCName, group.GroupCName)
                        .SetProperty(g => g.GroupEName, group.GroupEName)
                        .SetProperty(g => g.LogoutTime, group.LogoutTime)
                        .SetProperty(g => g.ModifiedTime, currentTime));
            }
            catch (Exception ex)
            {

            }
        }

        private void DeleteGroup(List<string> deletedata)
        {
            try
            {
                var query = _DBContext.pGroup.Where(s => deletedata.Contains(s.GroupId));
                query.ExecuteDelete();

            }
            catch (Exception ex)
            {

            }
        }
    }
}