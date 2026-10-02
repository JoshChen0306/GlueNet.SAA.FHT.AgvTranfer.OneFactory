using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SCP.Controllers;
using SCP.Models;

namespace SCP.Tests.TestInfrastructure
{
    /// <summary>
    /// SCP 快照測試用的 SQLite 記憶體資料庫（ToDo/20261001_清除二廠與舊專案殘留區域代號 T2）。
    /// - 每個測試各自 new 一個，連線存活期間資料庫才存在，測試之間互不影響（可平行執行）。
    /// - 不連任何真實資料庫。
    /// - oPort / oNeed / oRequire / oMission 在模型中是 keyless，EF 無法 Add()，
    ///   所以種資料一律走 ADO 參數化 INSERT。
    /// </summary>
    public sealed class ScpTestDb : IDisposable
    {
        private readonly SqliteConnection _connection;

        public agvDB_1400004Context Context { get; }

        public ScpTestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<agvDB_1400004Context>()
                .UseSqlite(_connection)
                .Options;

            Context = new agvDB_1400004Context(options);
            Context.Database.EnsureCreated();
        }

        // ── 種資料 ───────────────────────────────────────────────

        /// <summary>
        /// 新增站點。Block 取站號第一個字母、Port 取其後的數字（與一廠資料慣例一致）。
        /// </summary>
        public void AddPort(string stationNo, string haveFlag = "0", string useFlag = "Y", int priority = 50,
            string? rackId = "", string? workOrder = "", string? bgnToEnd = "", string? putTime = "",
            string area = "FHT1-1F")
        {
            Exec("INSERT INTO oPort (Area, Block, Port, StationNo, PanelCallShuttle, Priority, UseFlag, RackId, WorkOrder, HaveFlag, Remark, PutTime, BgnToEnd, MachineName) " +
                 "VALUES (@area, @block, @port, @station, 'Y', @priority, @useFlag, @rackId, @workOrder, @haveFlag, '200000,200000,0', @putTime, @bgnToEnd, @station)",
                ("@area", area),
                ("@block", stationNo.Substring(0, 1)),
                ("@port", int.Parse(stationNo.Substring(1))),
                ("@station", stationNo),
                ("@priority", priority),
                ("@useFlag", useFlag),
                ("@rackId", rackId),
                ("@workOrder", workOrder),
                ("@haveFlag", haveFlag),
                ("@putTime", putTime),
                ("@bgnToEnd", bgnToEnd));
        }

        public void AddNeed(string objStation, string endStation, string? assignFlag = "",
            string taskDateTime = "20260101000000000001", string? rackId = "", string? workOrder = "")
        {
            Exec("INSERT INTO oNeed (ObjStation, RackId, WorkOrder, EndStation, TaskSource, TaskDateTime, AssignFlag) " +
                 "VALUES (@obj, @rackId, @workOrder, @end, 'Web', @taskDateTime, @assignFlag)",
                ("@obj", objStation), ("@rackId", rackId), ("@workOrder", workOrder),
                ("@end", endStation), ("@taskDateTime", taskDateTime), ("@assignFlag", assignFlag));
        }

        public void AddRequire(string beginStation, string endStation, string? okFlag = "",
            string taskDateTime = "20260101000000000002")
        {
            Exec("INSERT INTO oRequire (TaskDateTime, ObjStation, SerialNo, BeginStation, EndStation, TaskSource, AssignFlag, OkFlag) " +
                 "VALUES (@taskDateTime, @begin, 0, @begin, @end, 'MCS', 'Y', @okFlag)",
                ("@taskDateTime", taskDateTime), ("@begin", beginStation), ("@end", endStation), ("@okFlag", okFlag));
        }

        public void AddMission(string beginStation, string endStation, string? okFlag = "",
            string taskDateTime = "20260101000000000003")
        {
            Exec("INSERT INTO oMission (TaskDateTime, SerialNo, BeginStation, EndStation, TaskSource, OkFlag) " +
                 "VALUES (@taskDateTime, 0, @begin, @end, 'MCS', @okFlag)",
                ("@taskDateTime", taskDateTime), ("@begin", beginStation), ("@end", endStation), ("@okFlag", okFlag));
        }

        public void AddRoute(string routeId, string sourceAreas, string targetAreas, string dispatchMode,
            string controlFlag = "Y")
        {
            Exec("INSERT INTO pRoute (RouteId, RouteName, SourceAreas, TargetAreas, ControlFlag, SortOrder, DispatchMode) " +
                 "VALUES (@routeId, @routeId, @source, @target, @controlFlag, 0, @mode)",
                ("@routeId", routeId), ("@source", sourceAreas), ("@target", targetAreas),
                ("@controlFlag", controlFlag), ("@mode", dispatchMode));
        }

        public void AddUserRoute(string userId, string routeId)
        {
            Exec("INSERT INTO pUserRoute (UserId, RouteId) VALUES (@userId, @routeId)",
                ("@userId", userId), ("@routeId", routeId));
        }

        // ── 讀結果 ───────────────────────────────────────────────

        public List<oNeed> Needs() => Context.oNeed.AsNoTracking().ToList();

        public oPort Port(string stationNo) => Context.oPort.AsNoTracking().Single(p => p.StationNo == stationNo);

        // ── 建立受測 Controller ──────────────────────────────────

        /// <summary>
        /// 建立 DispatchController。userId / groupId 為 null 時不帶對應 Claim（等同舊帳號沒有該資訊）。
        /// </summary>
        public DispatchController CreateDispatchController(string? userId = null, string? groupId = null,
            IDictionary<string, string?>? settings = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
                .Build();

            var claims = new List<Claim>();
            if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            if (groupId != null) claims.Add(new Claim(ClaimTypes.Role, groupId));

            return new DispatchController(configuration, Context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                    }
                }
            };
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }

        private void Exec(string sql, params (string Name, object? Value)[] parameters)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// 讀取 Controller 回傳結果的小工具（Controller 以匿名物件 new { message = ... } 回傳訊息）。
    /// </summary>
    public static class ActionResultExtensions
    {
        public static int StatusCodeOf(this IActionResult result) => result switch
        {
            ObjectResult o => o.StatusCode ?? StatusCodes.Status200OK,
            StatusCodeResult s => s.StatusCode,
            _ => throw new AssertFailedException("無法取得狀態碼的回傳型別：" + result.GetType().Name)
        };

        public static string? ValueOf(this IActionResult result, string propertyName)
        {
            var value = (result as ObjectResult)?.Value;
            return value?.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString();
        }

        public static string? MessageOf(this IActionResult result) => result.ValueOf("message");
    }
}
