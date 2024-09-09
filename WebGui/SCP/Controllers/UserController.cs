using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using SCP.Models;
using System.Collections.Immutable;

namespace SCP.Controllers
{

    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public UserController(ILogger<UserController> logger, agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
            _logger = logger;
        }

        public IActionResult Index(pUser _user)
        {

            var query = _DBContext.pUser;
            ViewBag.GroupName = _DBContext.pGroup.ToDictionary(g => g.GroupId, g => g.GroupCName);

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
                    pUser? user = JsonConvert.DeserializeObject<pUser>(item.Value.ToString());
                    InsertUser(user);

                }
            }

            if (updatedata.Count > 0)
            {
                foreach (var item in updatedata)
                {
                    pUser? user = JsonConvert.DeserializeObject<pUser>(item.Value.ToString());
                    UpdateUser(user);

                }
            }

            if (deletedata != null)
            {

                DeleteUser(deletedata);
            }

            return Ok();
        }

        private void InsertUser(pUser user)
        {

            try
            {
                string sql = "Insert into pUser (UserId, UserName,Password, GroupId,Mail,Tel, ModifiedTime) VALUES ({0}, {1}, {2}, {3},{4}, {5}, {6})";
                _ = _DBContext.Database.ExecuteSqlRaw(sql, user.UserId,user.UserName,user.UserId,user.GroupId,user.Mail,user.Tel,DateTime.Now.ToString("yyyMMddHHmmss"));
            }
            catch (Exception ex)
            {

            }
        }
        private void UpdateUser(pUser user)
        {
            var currenttime = DateTime.Now.ToString("yyyMMddHHmmss");
            try
            {
                _DBContext.pUser
                    .Where(u => u.UserId == user.UserId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(u => u.UserName, user.UserName)
                        .SetProperty(u => u.GroupId, user.GroupId)
                        .SetProperty(u => u.Mail, user.Mail)
                        .SetProperty(u => u.Tel, user.Tel)
                        .SetProperty(u => u.ModifiedTime, currenttime));
            }
            catch (Exception ex)
            {

            }
        }

        private void DeleteUser(List<string> deletedata)
        {
            try
            {
                var query = _DBContext.pUser.Where(s => deletedata.Contains(s.UserId));
                query.ExecuteDelete();

            }
            catch (Exception ex)
            {

            }
        }
    }
}