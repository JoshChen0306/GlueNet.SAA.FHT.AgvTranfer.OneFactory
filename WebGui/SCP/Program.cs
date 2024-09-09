using SCP.Commons;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SCP.Models;
using SCP.Hubs;
using SCP.Services;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Razor;


var builder = WebApplication.CreateBuilder(args);
var path = Directory.GetCurrentDirectory();
var connectionString = builder.Configuration.GetConnectionString("DBContext");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File($"C:\\Logs\\AMS\\{DateTime.Now.ToString("yyyyMM")}\\{DateTime.Now.ToString("yyyyMMdd")}_AMS.txt")
    .CreateLogger();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add services to the container.
builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization();
// 配置請求本地化選項
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("zh-TW")
    };

    options.DefaultRequestCulture = new RequestCulture("zh-TW");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
});


builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddDbContext<agvDB_1400004Context>(option => option.UseSqlServer(connectionString));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(option =>
{
    option.AccessDeniedPath = new PathString("/Account/AccessDenied");
    //未登入時會自動導到這個網址
    option.LoginPath = new PathString("/Account/Login");

});
builder.Services.AddSignalR();
builder.Services.AddHostedService<Service>();

var app = builder.Build();

app.UseRequestLocalization();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<CommonHub>("/CommonHub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=home}/{action=index}/{id?}");

app.Run();
