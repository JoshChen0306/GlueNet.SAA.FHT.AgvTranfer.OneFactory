using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SCP.Models;
using System.Security.Claims;

namespace SCP.Controllers
{
    [Authorize(Roles = "1")]
    public class RouteController : Controller
    {
        private readonly ILogger<RouteController> _logger;
        private readonly agvDB_1400004Context _DBContext;

        public RouteController(ILogger<RouteController> logger, agvDB_1400004Context DBContext)
        {
            _logger = logger;
            _DBContext = DBContext;
        }

        /// <summary>
        /// 路線權限管理頁面
        /// </summary>
        public IActionResult Index()
        {
            // 取得所有使用者清單
            ViewBag.UserList = _DBContext.pUser
                .OrderBy(u => u.UserId)
                .Select(u => new SelectListItem 
                { 
                    Value = u.UserId, 
                    Text = u.UserId + "." + u.UserName 
                });

            // 取得所有路線定義
            ViewBag.Routes = _DBContext.pRoute
                .Where(r => r.ControlFlag == "Y")
                .OrderBy(r => r.SortOrder)
                .ToList();

            return View();
        }

        /// <summary>
        /// 取得所有路線定義
        /// </summary>
        [HttpGet]
        public IActionResult GetAllRoutes()
        {
            try
            {
                var routes = _DBContext.pRoute
                    .Where(r => r.ControlFlag == "Y")
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new
                    {
                        r.RouteId,
                        r.RouteName,
                        r.SourceFloor,
                        r.TargetFloor,
                        r.RouteType,
                        r.SourceAreas,
                        r.TargetAreas
                    })
                    .ToList();

                return Json(routes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllRoutes failed");
                return StatusCode(500, new { message = "取得路線定義失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 取得指定使用者的路線權限
        /// </summary>
        [HttpGet]
        public IActionResult GetUserRoutes(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { message = "請選擇使用者" });
                }

                var userRoutes = _DBContext.pUserRoute
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.RouteId)
                    .ToList();

                return Json(userRoutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserRoutes failed for userId: {UserId}", userId);
                return StatusCode(500, new { message = "取得使用者路線權限失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 儲存使用者路線權限
        /// </summary>
        [HttpPost]
        public IActionResult SaveUserRoutes([FromBody] SaveUserRoutesRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.UserId))
                {
                    return BadRequest(new { message = "請選擇使用者" });
                }

                // 刪除該使用者現有的所有路線權限
                var existingRoutes = _DBContext.pUserRoute
                    .Where(ur => ur.UserId == request.UserId)
                    .ToList();

                if (existingRoutes.Any())
                {
                    _DBContext.pUserRoute.RemoveRange(existingRoutes);
                }

                // 新增選取的路線權限
                if (request.RouteIds != null && request.RouteIds.Any())
                {
                    foreach (var routeId in request.RouteIds)
                    {
                        _DBContext.pUserRoute.Add(new pUserRoute
                        {
                            UserId = request.UserId,
                            RouteId = routeId
                        });
                    }
                }

                _DBContext.SaveChanges();

                _logger.LogInformation("User {UserId} routes updated: {Routes}", 
                    request.UserId, 
                    string.Join(", ", request.RouteIds ?? new List<string>()));

                return Ok(new { message = "儲存成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveUserRoutes failed for userId: {UserId}", request?.UserId);
                return StatusCode(500, new { message = "儲存失敗", error = ex.Message });
            }
        }

        /// <summary>
        /// 取得當前使用者的可用區域（供 DispatchController 使用）
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetCurrentUserAllowedAreas()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    // 未登入使用者，返回空陣列
                    return Json(new List<string>());
                }

                // 取得使用者的路線權限
                var userRoutes = _DBContext.pUserRoute
                    .Where(ur => ur.UserId == userId)
                    .Join(_DBContext.pRoute.Where(r => r.ControlFlag == "Y"), 
                          ur => ur.RouteId, 
                          r => r.RouteId, 
                          (ur, r) => r)
                    .ToList();

                // 取得使用者可用的所有區域
                var allowedAreas = userRoutes
                    .SelectMany(r => 
                    {
                        var areas = new List<string>();
                        if (!string.IsNullOrEmpty(r.SourceAreas))
                            areas.AddRange(r.SourceAreas.Split(',').Select(a => a.Trim()));
                        if (!string.IsNullOrEmpty(r.TargetAreas))
                            areas.AddRange(r.TargetAreas.Split(',').Select(a => a.Trim()));
                        return areas;
                    })
                    .Distinct()
                    .ToList();

                return Json(allowedAreas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCurrentUserAllowedAreas failed");
                return StatusCode(500, new { message = "取得可用區域失敗", error = ex.Message });
            }
        }
    }

    /// <summary>
    /// 儲存使用者路線權限的請求模型
    /// </summary>
    public class SaveUserRoutesRequest
    {
        public string UserId { get; set; } = null!;
        public List<string>? RouteIds { get; set; }
    }
}
