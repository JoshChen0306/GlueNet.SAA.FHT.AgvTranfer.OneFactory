using Microsoft.EntityFrameworkCore;
using SCP.Models;

namespace SCP.Services
{
    /// <summary>
    /// 自動派送服務 - 路線6: M區 → K區
    /// 當 M區有 status=3 的物料，且 K區有 status=0 的空位時，自動建立派送任務
    /// </summary>
    public class AutoDispatchService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoDispatchService> _logger;
        private readonly IConfiguration _configuration;
        
        // 檢查間隔（預設 30 秒）
        private readonly TimeSpan _checkInterval;
        
        // 是否啟用自動派送
        private readonly bool _enabled;

        public AutoDispatchService(
            IServiceScopeFactory scopeFactory, 
            ILogger<AutoDispatchService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
            
            // 從設定檔讀取參數
            var autoDispatchSettings = _configuration.GetSection("AutoDispatch");
            _enabled = autoDispatchSettings.GetValue<bool>("Enabled", true);
            var intervalSeconds = autoDispatchSettings.GetValue<int>("IntervalSeconds", 30);
            _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
            
            _logger.LogInformation($"AutoDispatchService initialized. Enabled: {_enabled}, Interval: {intervalSeconds}s");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("AutoDispatchService is disabled.");
                return;
            }

            _logger.LogInformation("AutoDispatchService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndDispatchMToK(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoDispatch M→K check failed");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("AutoDispatchService stopped.");
        }

        /// <summary>
        /// 檢查並執行 M區 → K區 自動派送
        /// 條件：M區有 status=3 的物料 且 K區有 status=0 的空位
        /// 優先順序：依 PutTime 時間優先 (FIFO)
        /// </summary>
        private async Task CheckAndDispatchMToK(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<agvDB_1400004Context>();

            // 取得已有待處理任務的起點站（避免重複派送）
            var pendingBeginStations = await dbContext.oNeed
                .Where(n => n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")
                .Select(n => n.ObjStation)
                .ToListAsync(stoppingToken);

            // 加入 oRequire 和 oMission 中進行中的起點站
            var pendingFromRequire = await dbContext.oRequire
                .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                .Select(r => r.BeginStation)
                .ToListAsync(stoppingToken);

            var pendingFromMission = await dbContext.oMission
                .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                .Select(m => m.BeginStation)
                .ToListAsync(stoppingToken);

            var allPendingBeginStations = pendingBeginStations
                .Concat(pendingFromRequire)
                .Concat(pendingFromMission)
                .Distinct()
                .ToHashSet();

            // 取得已有待處理任務的終點站（避免重複指派到同一位置）
            var pendingEndStations = await dbContext.oNeed
                .Where(n => n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")
                .Select(n => n.EndStation)
                .ToListAsync(stoppingToken);

            var pendingEndFromRequire = await dbContext.oRequire
                .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                .Select(r => r.EndStation)
                .ToListAsync(stoppingToken);

            var pendingEndFromMission = await dbContext.oMission
                .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                .Select(m => m.EndStation)
                .ToListAsync(stoppingToken);

            var allPendingEndStations = pendingEndStations
                .Concat(pendingEndFromRequire)
                .Concat(pendingEndFromMission)
                .Distinct()
                .ToHashSet();

            // 找 M區 status=3 的物料（依時間優先 FIFO）
            // 排除已有任務的起點站
            var mMaterials = await dbContext.oPort
                .Where(p => p.Block == "M" &&
                            p.HaveFlag == "3" &&
                            p.UseFlag == "Y" &&
                            !allPendingBeginStations.Contains(p.StationNo))
                .OrderBy(p => p.PutTime)  // 時間優先 (FIFO)
                .ToListAsync(stoppingToken);

            if (!mMaterials.Any())
            {
                // 沒有可派送的物料
                return;
            }

            // 找 K區 status=0 的空位
            // 排除已有任務的終點站
            var kSlots = await dbContext.oPort
                .Where(p => p.Block == "K" &&
                            p.HaveFlag == "0" &&
                            (p.BgnToEnd == null || p.BgnToEnd == "") &&
                            p.UseFlag == "Y" &&
                            !allPendingEndStations.Contains(p.StationNo))
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.Port)
                .ToListAsync(stoppingToken);

            if (!kSlots.Any())
            {
                // K區沒有空位
                return;
            }

            // 依序配對 M區物料 和 K區空位
            int dispatchCount = Math.Min(mMaterials.Count, kSlots.Count);
            
            for (int i = 0; i < dispatchCount; i++)
            {
                var mMaterial = mMaterials[i];
                var kSlot = kSlots[i];


                // 參照 DispatchController.InsertoNeed，改用 Raw SQL 插入
                string sql = "INSERT INTO oNeed (ObjStation,RackId,WorkOrder,EndStation,TaskSource,TaskDateTime,AssignFlag) VALUES({0},{1},{2},{3},{4},{5},{6})";
                await dbContext.Database.ExecuteSqlRawAsync(sql, 
                    mMaterial.StationNo, 
                    mMaterial.RackId ?? "", 
                    mMaterial.WorkOrder ?? "", 
                    kSlot.StationNo, 
                    "Auto", 
                    DateTime.Now.ToString("yyyyMMddHHmmssffffff"), 
                    "");

                _logger.LogInformation($"AutoDispatch: {mMaterial.StationNo} → {kSlot.StationNo} (PutTime: {mMaterial.PutTime})");
            }

            // Raw SQL 已經執行，不需要 SaveChanges
            // var savedCount = await dbContext.SaveChangesAsync(stoppingToken);
            
            if (dispatchCount > 0)
            {
                _logger.LogInformation($"AutoDispatch completed: {dispatchCount} task(s) created.");
            }
        }
    }
}
