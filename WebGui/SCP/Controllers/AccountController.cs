using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SCP.Models;
using Microsoft.AspNetCore.Authorization;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;


namespace SCP.Controllers
{

    public class AccountController : Controller
    {
        private readonly agvDB_1400004Context _DBContext;
        public AccountController(agvDB_1400004Context DBContext)
        {
            _DBContext = DBContext;
        }
        [Authorize]
        public IActionResult Index()
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            pUser query = _DBContext.pUser.Where(u => u.UserId == userId).FirstOrDefault();
            return View(query);
        }

        //登入頁面
        public IActionResult Login(string returnurl = null)
        {
            return View();
        }
        //拒絕存取頁面
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([Bind("UserId", "Password")] pUser pUser, string returnUrl)
        {
            string errorMessage = string.Empty;


            //var user = _amsDBContext.pUser.Where(u => u.UserId == pUser.UserId && u.Password == pUser.Password).FirstOrDefault();
            var user = _DBContext.pUser.Where(u => u.UserId == pUser.UserId && u.Password == pUser.Password)
                .Join(_DBContext.pGroup, a => a.GroupId, g => g.GroupId, (a, g) =>
                new
                {
                    UserId = a.UserId,
                    UserName = a.UserName,
                    GroupEName = g.GroupEName,
                    GroupCName = g.GroupCName,
                    LogoutTime = g.LogoutTime
                }).FirstOrDefault();

            if (user == null)
            {
                errorMessage = "帳號或密碼錯誤";
            }
            else
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name,user.UserName),
                    new Claim(ClaimTypes.NameIdentifier,user.UserId),
                    new Claim(ClaimTypes.Role,user.GroupEName)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    //持續性cookie設定
                    //IsPersistent = true,

                    //設定cookie持續時間
                    ExpiresUtc = DateTime.UtcNow.AddMinutes(Convert.ToDouble(user.LogoutTime))
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                string expiresUtc = DateTimeOffset.UtcNow.AddMinutes(Convert.ToDouble(user.LogoutTime)).ToString("yyyy-MM-ddTHH:mm:ssZ");
                return RedirectToAction("Index", "Home", new { returnUrl, expiresUtc });
            }

            ViewBag.errorMessage = errorMessage;
            return View();
        }

        [HttpPost]
        public IActionResult DataChange([FromBody] pUser updatedata)
        {
            if (updatedata != null)
            {
                UpdateUser(updatedata);
            }

            return Ok();
        }

        private void UpdateUser(pUser? user)
        {
            var currentTime = DateTime.Now.ToString("yyyyMMddHHmmss ");
            try
            {
                _DBContext.pUser
                    .Where(u => u.UserId == user.UserId)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(u => u.UserName, user.UserName)
                        .SetProperty(u => u.Password, user.Password)
                        .SetProperty(u => u.Mail, user.Mail)
                        .SetProperty(u => u.Tel, user.Tel));
            }
            catch (Exception ex)
            {

            }

        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
