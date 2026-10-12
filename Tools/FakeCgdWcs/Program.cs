using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// 假的中光電 WCS（T25）：模擬「中光電儲位交握api.pdf」三支 API，供 ACC 介接整合測試與 T20 使用。
//
// 用法：dotnet run --project Tools/FakeCgdWcs [-- http://localhost:5080/]
//   ACC FHtSetting.config 的 CgdWcsSettings BaseUrl 指向同一位址並設 Enable="true"。
//
// 儲位 API（與規格相同）：
//   GET  /api/v1/wcs/locations/{binCode}/status   → code 0＋data.itemCode（儲位空時 itemCode 為空字串）
//   POST /api/v1/wcs/locations/{binCode}/Mapping  → body { itemCode }；已綁定回 1（已佔用）
//   POST /api/v1/wcs/locations/{binCode}/release  → 已空回 1（已釋放）
//
// 測試控制（模擬中光電放料與異常）：
//   POST /__admin/bins/{binCode}?itemCode=X       → 設定儲位物料（itemCode 空 = 清空）
//   POST /__admin/fault?code=2&delayMs=0&count=1  → 接下來 count 次儲位 API 回指定 code（code 省略則只延遲，可模擬逾時）
//   GET  /__admin/state                           → 目前儲位內容與最近 100 筆請求紀錄
//   POST /__admin/reset                           → 清空儲位、異常設定與紀錄

var prefix = args.Length > 0 ? args[0] : "http://localhost:5080/";
var bins = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var requestLog = new ConcurrentQueue<string>();
var faultLock = new object();
int faultCount = 0;
int? faultCode = null;
int faultDelayMs = 0;

var listener = new HttpListener();
listener.Prefixes.Add(prefix);
listener.Start();
Console.WriteLine($"[FakeCgdWcs] 監聽 {prefix}（Ctrl+C 結束）");

var locationRoute = new Regex(@"^/api/v1/wcs/locations/(?<bin>[^/]+)/(?<action>status|Mapping|release)$", RegexOptions.IgnoreCase);
var adminBinRoute = new Regex(@"^/__admin/bins/(?<bin>[^/]+)$", RegexOptions.IgnoreCase);

while (listener.IsListening)
{
    var ctx = await listener.GetContextAsync();
    _ = Task.Run(() => HandleAsync(ctx));
}

async Task HandleAsync(HttpListenerContext ctx)
{
    var req = ctx.Request;
    string path = Uri.UnescapeDataString(req.Url!.AbsolutePath);
    string body = "";
    using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
    {
        body = await reader.ReadToEndAsync();
    }

    try
    {
        var loc = locationRoute.Match(path);
        if (loc.Success)
        {
            await HandleLocationAsync(ctx, req.HttpMethod, loc.Groups["bin"].Value, loc.Groups["action"].Value.ToLowerInvariant(), body);
            return;
        }

        var adminBin = adminBinRoute.Match(path);
        if (adminBin.Success && req.HttpMethod == "POST")
        {
            string bin = adminBin.Groups["bin"].Value;
            string item = req.QueryString["itemCode"] ?? "";
            if (item.Length == 0) bins.TryRemove(bin, out _); else bins[bin] = item;
            Log($"[admin] 設定 {bin} itemCode={item}");
            await WriteJson(ctx, 200, new { code = 0, message = "ok" });
            return;
        }

        if (path.Equals("/__admin/fault", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "POST")
        {
            lock (faultLock)
            {
                faultCode = int.TryParse(req.QueryString["code"], out int c) ? c : null;
                faultDelayMs = int.TryParse(req.QueryString["delayMs"], out int d) ? d : 0;
                faultCount = int.TryParse(req.QueryString["count"], out int n) ? n : 1;
            }
            Log($"[admin] 接下來 {faultCount} 次回 code={faultCode?.ToString() ?? "(正常)"}，延遲 {faultDelayMs}ms");
            await WriteJson(ctx, 200, new { code = 0, message = "ok" });
            return;
        }

        if (path.Equals("/__admin/state", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJson(ctx, 200, new { bins, requests = requestLog.ToArray() });
            return;
        }

        if (path.Equals("/__admin/reset", StringComparison.OrdinalIgnoreCase) && req.HttpMethod == "POST")
        {
            bins.Clear();
            while (requestLog.TryDequeue(out _)) { }
            lock (faultLock) { faultCount = 0; faultCode = null; faultDelayMs = 0; }
            await WriteJson(ctx, 200, new { code = 0, message = "ok" });
            return;
        }

        await WriteJson(ctx, 404, new { code = 2, message = $"not found: {req.HttpMethod} {path}" });
    }
    catch (Exception ex)
    {
        Log($"[error] {req.HttpMethod} {path}：{ex.Message}");
        await WriteJson(ctx, 500, new { code = 2, message = ex.Message });
    }
}

async Task HandleLocationAsync(HttpListenerContext ctx, string method, string bin, string action, string body)
{
    int? injectedCode = null;
    int delay = 0;
    lock (faultLock)
    {
        if (faultCount > 0)
        {
            faultCount--;
            injectedCode = faultCode;
            delay = faultDelayMs;
        }
    }

    if (delay > 0) await Task.Delay(delay);
    if (injectedCode != null)
    {
        Log($"{method} {bin}/{action} → 模擬異常 code={injectedCode}");
        await WriteJson(ctx, 200, new { code = injectedCode, message = "ERROR (fake)" });
        return;
    }

    switch (action)
    {
        case "status" when method == "GET":
            string item = bins.TryGetValue(bin, out var v) ? v : "";
            Log($"GET {bin}/status → itemCode={item}");
            await WriteJson(ctx, 200, new { code = 0, message = "success", data = new { itemCode = item } });
            return;

        case "mapping" when method == "POST":
            string newItem = "";
            try
            {
                using var doc = JsonDocument.Parse(body.Length == 0 ? "{}" : body);
                newItem = doc.RootElement.TryGetProperty("itemCode", out var p) ? p.GetString() ?? "" : "";
            }
            catch (JsonException ex)
            {
                Log($"POST {bin}/Mapping → body 不是 JSON：{ex.Message}");
                await WriteJson(ctx, 200, new { code = 2, message = "invalid body" });
                return;
            }

            if (bins.TryGetValue(bin, out var existing))
            {
                Log($"POST {bin}/Mapping itemCode={newItem} → 已佔用（{existing}）");
                await WriteJson(ctx, 200, new { code = 1, message = "occupied" });
                return;
            }

            bins[bin] = newItem;
            Log($"POST {bin}/Mapping itemCode={newItem} → 成功");
            await WriteJson(ctx, 200, new { code = 0, message = "success" });
            return;

        case "release" when method == "POST":
            bool removed = bins.TryRemove(bin, out _);
            Log($"POST {bin}/release → {(removed ? "成功" : "已釋放")}");
            await WriteJson(ctx, 200, new { code = removed ? 0 : 1, message = removed ? "success" : "already released" });
            return;

        default:
            await WriteJson(ctx, 405, new { code = 2, message = $"method not allowed: {method} {action}" });
            return;
    }
}

void Log(string message)
{
    string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
    Console.WriteLine(line);
    requestLog.Enqueue(line);
    while (requestLog.Count > 100 && requestLog.TryDequeue(out _)) { }
}

static async Task WriteJson(HttpListenerContext ctx, int status, object payload)
{
    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.ContentLength64 = bytes.Length;
    await ctx.Response.OutputStream.WriteAsync(bytes);
    ctx.Response.Close();
}
