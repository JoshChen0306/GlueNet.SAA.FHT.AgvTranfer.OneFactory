using SCP.Models;

namespace SCP.Services
{
    /// <summary>
    /// 自動派送服務：每輪依 appsettings AutoDispatch:Routes 交給 AutoDispatchRouteEngine 執行。
    /// 設定檔沒有 Routes 區段時沿用路線6 M→K（重構前的唯一路線）。
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

        // 路線規則
        private readonly List<AutoDispatchRoute> _routes;

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
            _routes = ReadRoutes(autoDispatchSettings);

            _logger.LogInformation($"AutoDispatchService initialized. Enabled: {_enabled}, Interval: {intervalSeconds}s, Routes: {string.Join(", ", _routes.Select(r => r.Name))}");
        }

        private static List<AutoDispatchRoute> ReadRoutes(IConfigurationSection autoDispatchSettings)
        {
            var routesSection = autoDispatchSettings.GetSection("Routes");
            if (!routesSection.Exists())
            {
                return new List<AutoDispatchRoute> { AutoDispatchRoute.LegacyMToK() };
            }
            return routesSection.Get<List<AutoDispatchRoute>>() ?? new List<AutoDispatchRoute>();
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
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoDispatch check failed");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("AutoDispatchService stopped.");
        }

        /// <summary>執行一輪自動派送，回傳建立的任務數。</summary>
        public async Task<int> RunOnceAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<agvDB_1400004Context>();
            var engine = new AutoDispatchRouteEngine(dbContext, _logger);
            return await engine.ExecuteAsync(_routes, stoppingToken);
        }
    }
}
