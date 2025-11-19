using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using SCP.Hubs;

namespace SCP.Services
{
    public class Service : IHostedService
    {
        private readonly IHubContext<CommonHub> _hub;
        private string _connectionString;
        private List<SqlDependency> _dependency = new List<SqlDependency>();
        public Service(IConfiguration configuration, IHubContext<CommonHub> hub)
        {
            _hub = hub;
            _connectionString = configuration.GetConnectionString("DBContext");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            SqlDependency.Start(_connectionString);
            RegisterDependency("select HaveFlag,BgnToEnd from dbo.oPort", OnTracChange);
            RegisterDependency("select PosX,PosY from dbo.oShuttle", OnAgvChange);
            RegisterDependency("select Battery,Status,LastStation,BeginStation,EndStation from dbo.oShuttle", OnAgvStatusChange);
            RegisterDependency("select TaskDateTime from dbo.ubMission", OnTotalTaskChange);
            RegisterDependency("select AssignFlag,OkFlag from dbo.oRequire", OnDispatchChange);
            RegisterDependency("select OkFlag from dbo.oMission", OnMissionChange);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            SqlDependency.Stop(_connectionString);
            return Task.CompletedTask;
        }

        private void RegisterDependency(string _sql, OnChangeEventHandler eventHandler)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand(_sql, connection))
                {
                    var dependency = new SqlDependency(command);
                    dependency.OnChange += eventHandler;
                    _dependency.Add(dependency);

                    // 執行命令以註冊 SqlDependency 物件
                    command.ExecuteReader();
                }
            }
        }

        private async void OnTracChange(object sender, SqlNotificationEventArgs e)
        {
            RegisterDependency("select HaveFlag,BgnToEnd from dbo.oPort", OnTracChange);
            await _hub.Clients.All.SendAsync("SendTracChange");
        }

        private async void OnAgvChange(object sender, SqlNotificationEventArgs e)
        {
            RegisterDependency("select PosX,PosY from dbo.oShuttle", OnAgvChange);
            await _hub.Clients.All.SendAsync("SendAgvChange");
        }

        private async void OnAgvStatusChange(object sender, SqlNotificationEventArgs e)
        {
            RegisterDependency("select Battery,Status,LastStation,BeginStation,EndStation from dbo.oShuttle", OnAgvStatusChange);
            await _hub.Clients.All.SendAsync("SendAgvStatusChange");
        }

        private async void OnTotalTaskChange(object sender, SqlNotificationEventArgs e)
        {
            RegisterDependency("select TaskDateTime from dbo.ubMission", OnTotalTaskChange);
            await _hub.Clients.All.SendAsync("SendTotalTaskChange");
        }
        private async void OnDispatchChange(object sender, SqlNotificationEventArgs e)
        {
            RegisterDependency("select AssignFlag,OkFlag from dbo.oRequire", OnDispatchChange);
            await _hub.Clients.All.SendAsync("SendDispatchChange");
        }
        private async void OnMissionChange(object sender, SqlNotificationEventArgs e)
        {
            RegisterDependency("select OkFlag from dbo.oMission", OnMissionChange);
            await _hub.Clients.All.SendAsync("SendMissionChange");
        }
    }
}
