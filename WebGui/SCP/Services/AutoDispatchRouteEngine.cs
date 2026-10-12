using Microsoft.EntityFrameworkCore;
using SCP.Models;

namespace SCP.Services
{
    /// <summary>
    /// 自動派送規則引擎：依路線規則逐條檢查來源區物料與目的區空位，配對後建立 oNeed。
    /// 由 AutoDispatchService 每輪建立 scope 後呼叫；不持有狀態，可直接以測試資料庫驗證。
    /// </summary>
    public class AutoDispatchRouteEngine
    {
        private readonly agvDB_1400004Context _dbContext;
        private readonly ILogger _logger;

        public AutoDispatchRouteEngine(agvDB_1400004Context dbContext, ILogger logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>依序執行每條路線，回傳本輪建立的任務數。</summary>
        public async Task<int> ExecuteAsync(IEnumerable<AutoDispatchRoute> routes, CancellationToken stoppingToken)
        {
            var routeList = routes.ToList();
            if (routeList.Count == 0)
            {
                _logger.LogWarning("AutoDispatch: 未設定任何 Routes，本輪不派送");
                return 0;
            }

            int total = 0;
            foreach (var route in routeList)
            {
                if (!route.Enabled)
                {
                    continue;
                }
                if (route.SourceBlocks.Count == 0 || route.TargetBlocks.Count == 0)
                {
                    _logger.LogWarning($"AutoDispatch: 路線 {route.Name} 未設定 SourceBlocks 或 TargetBlocks，略過");
                    continue;
                }
                total += await ExecuteRouteAsync(route, stoppingToken);
            }
            return total;
        }

        /// <summary>
        /// 檢查並執行單一路線
        /// 條件：來源區有 SourceHaveFlags（預設 3）的物料 且 目的區有 status=0 的空位
        /// 優先順序：依 PutTime 時間優先 (FIFO)
        /// </summary>
        private async Task<int> ExecuteRouteAsync(AutoDispatchRoute route, CancellationToken stoppingToken)
        {
            var sourceHaveFlags = route.SourceHaveFlags.Count > 0 ? route.SourceHaveFlags : new List<string> { "3" };

            // 取得已有待處理任務的起點站（避免重複派送）
            var pendingBeginStations = await _dbContext.oNeed
                .Where(n => n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")
                .Select(n => n.ObjStation)
                .ToListAsync(stoppingToken);

            // 加入 oRequire 和 oMission 中進行中的起點站
            var pendingFromRequire = await _dbContext.oRequire
                .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                .Select(r => r.BeginStation)
                .ToListAsync(stoppingToken);

            var pendingFromMission = await _dbContext.oMission
                .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                .Select(m => m.BeginStation)
                .ToListAsync(stoppingToken);

            var allPendingBeginStations = pendingBeginStations
                .Concat(pendingFromRequire)
                .Concat(pendingFromMission)
                .Distinct()
                .ToHashSet();

            // 取得已有待處理任務的終點站（避免重複指派到同一位置）
            var pendingEndStations = await _dbContext.oNeed
                .Where(n => n.AssignFlag == null || n.AssignFlag == "" || n.AssignFlag == "Y")
                .Select(n => n.EndStation)
                .ToListAsync(stoppingToken);

            var pendingEndFromRequire = await _dbContext.oRequire
                .Where(r => r.OkFlag == null || r.OkFlag == "" || r.OkFlag == "R")
                .Select(r => r.EndStation)
                .ToListAsync(stoppingToken);

            var pendingEndFromMission = await _dbContext.oMission
                .Where(m => m.OkFlag == null || m.OkFlag == "" || m.OkFlag == "Y" || m.OkFlag == "R")
                .Select(m => m.EndStation)
                .ToListAsync(stoppingToken);

            var allPendingEndStations = pendingEndStations
                .Concat(pendingEndFromRequire)
                .Concat(pendingEndFromMission)
                .Distinct()
                .ToHashSet();

            // 找來源區有貨的站點（依時間優先 FIFO）
            // 排除已有任務的起點站
            var sourceMaterials = await _dbContext.oPort
                .Where(p => route.SourceBlocks.Contains(p.Block) &&
                            sourceHaveFlags.Contains(p.HaveFlag) &&
                            p.UseFlag == "Y" &&
                            !allPendingBeginStations.Contains(p.StationNo))
                .OrderBy(p => p.PutTime)  // 時間優先 (FIFO)
                .ToListAsync(stoppingToken);

            if (route.SensorGuard)
            {
                sourceMaterials = sourceMaterials.Where(p => IsSourceConsistent(route, p)).ToList();
            }

            if (!sourceMaterials.Any())
            {
                // 沒有可派送的物料
                return 0;
            }

            // 找目的區 status=0 的空位
            // 排除已有任務的終點站
            var targetSlots = await _dbContext.oPort
                .Where(p => route.TargetBlocks.Contains(p.Block) &&
                            p.HaveFlag == "0" &&
                            (p.BgnToEnd == null || p.BgnToEnd == "") &&
                            p.UseFlag == "Y" &&
                            !allPendingEndStations.Contains(p.StationNo))
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.Port)
                .ToListAsync(stoppingToken);

            if (route.SensorGuard)
            {
                targetSlots = targetSlots.Where(p => IsTargetConsistent(route, p)).ToList();
            }

            if (!targetSlots.Any())
            {
                // 目的區沒有空位
                return 0;
            }

            // 依序配對來源區物料 和 目的區空位
            int dispatchCount = Math.Min(sourceMaterials.Count, targetSlots.Count);

            for (int i = 0; i < dispatchCount; i++)
            {
                var material = sourceMaterials[i];
                var slot = targetSlots[i];

                // 參照 DispatchController.InsertoNeed，改用 Raw SQL 插入
                string sql = "INSERT INTO oNeed (ObjStation,RackId,WorkOrder,EndStation,TaskSource,TaskDateTime,AssignFlag) VALUES({0},{1},{2},{3},{4},{5},{6})";
                await _dbContext.Database.ExecuteSqlRawAsync(sql,
                    material.StationNo,
                    material.RackId ?? "",
                    material.WorkOrder ?? "",
                    slot.StationNo,
                    "Auto",
                    DateTime.Now.ToString("yyyyMMddHHmmssffffff"),
                    "");

                _logger.LogInformation($"AutoDispatch: {material.StationNo} → {slot.StationNo} (PutTime: {material.PutTime})");
            }

            if (dispatchCount > 0)
            {
                _logger.LogInformation($"AutoDispatch completed: {dispatchCount} task(s) created.");
            }
            return dispatchCount;
        }

        /// <summary>sensor 防呆（來源）：HaveFlag=3 表示有工單，WorkOrder 卻是空的 → 不一致。</summary>
        private bool IsSourceConsistent(AutoDispatchRoute route, oPort port)
        {
            if (port.HaveFlag == "3" && string.IsNullOrEmpty(port.WorkOrder))
            {
                _logger.LogWarning($"AutoDispatch: 路線 {route.Name} 來源 {port.StationNo} HaveFlag=3 但沒有 WorkOrder，狀態不一致不派車");
                return false;
            }
            return true;
        }

        /// <summary>sensor 防呆（目的）：HaveFlag=0 表示空位，RackId/WorkOrder 卻有殘留 → 不一致。</summary>
        private bool IsTargetConsistent(AutoDispatchRoute route, oPort port)
        {
            if (!string.IsNullOrEmpty(port.RackId) || !string.IsNullOrEmpty(port.WorkOrder))
            {
                _logger.LogWarning($"AutoDispatch: 路線 {route.Name} 目的 {port.StationNo} HaveFlag=0 但仍有 RackId={port.RackId}/WorkOrder={port.WorkOrder}，狀態不一致不指派");
                return false;
            }
            return true;
        }
    }
}
