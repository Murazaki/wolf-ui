using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Resources.WolfAPI;
using WolfUI.Interfaces;
using WolfUI.Tasks;

namespace Godot.DependencyInjection
{
    public class Test : BackgroundService
    {
        private readonly ILogger<Test> _logger;
        private readonly NSwagWolfApi.NSwagWolfApi _api;
        private readonly NSwagDocker.NSwagDocker _docker;

        public Test(ILogger<Test> logger, 
            NSwagWolfApi.NSwagWolfApi api, 
            NSwagDocker.NSwagDocker docker)
        {
            _logger = logger;
            _api = api;
            _docker = docker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

        }
    }
    
    internal partial class DependencyInjection
    {
        static partial void ConfigureHostConfiguration(IConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.AddEnvironmentVariables("WOLF_");
        }

        static partial void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole());
            services.AddScoped<System.Net.Http.HttpClient>(p =>
            {
                return new System.Net.Http.HttpClient(new SocketsHttpHandler
                {
                    ConnectCallback = async (ctx, token) =>
                    {
                        var endpointPath = System.Environment.GetEnvironmentVariable("WOLF_SOCKET_PATH") ??
                                           "/etc/wolf/cfg/wolf.sock";
                        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                        var endpoint = new UnixDomainSocketEndPoint(endpointPath);
                        await socket.ConnectAsync(endpoint, token);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                });
            });
            
            services.AddSingleton<WolfApiEventsTask>()
                  .AddSingleton<IApiEventPublisher>(p => p.GetRequiredService<WolfApiEventsTask>())
                  .AddHostedService(p => p.GetRequiredService<WolfApiEventsTask>());
            
            services.AddSingleton<NSwagDocker.NSwagDocker>()
                .AddSingleton<IDockerApiClient>(p => p.GetRequiredService<NSwagDocker.NSwagDocker>())
                .AddSingleton<IDockerEventPublisher>(p => p.GetRequiredService<NSwagDocker.NSwagDocker>());
            
            services.AddSingleton<NSwagWolfApi.NSwagWolfApi>()
                .AddHostedService<Test>();
        }
    }
}