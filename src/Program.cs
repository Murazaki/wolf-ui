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
using WolfUI.Tasks;

namespace Godot.DependencyInjection
{
    public class Test : BackgroundService
    {
        private readonly ILogger<Test> _logger;
        private readonly NSwagWolfApi.NSwagWolfApi _api;
        private readonly NSwagDocker.NSwagDocker _docker;
        private readonly WolfApiEventsTask _wolfApiEventsTask;

        public Test(ILogger<Test> logger, NSwagWolfApi.NSwagWolfApi api, NSwagDocker.NSwagDocker docker, WolfApiEventsTask wolfApiEventsTask)
        {
            _logger = logger;
            _api = api;
            _docker = docker;
            _wolfApiEventsTask = wolfApiEventsTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _wolfApiEventsTask.DockerPulledImageEvent += (image, success) => _logger.LogInformation($"Docker pulled image: {image} - {success}");
            _wolfApiEventsTask.DockerPullingImageEvent += image => _logger.LogInformation($"Docker pulling image: {image}");
            
            try
            {
                var a = await _docker.InspectAsync("ghcr.io/games-on-whales/wolf-ui:main", stoppingToken);
                GD.Print(a);
            }
            catch (Exception e)
            {
                GD.Print(e.Message);
            }
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
            
            services.AddHttpClient("WolfApi").ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new SocketsHttpHandler
                    {
                        ConnectCallback = async (ctx, token) =>
                        {
                            var endpointPath = System.Environment.GetEnvironmentVariable("WOLF_SOCKET_PATH") ??
                                               "/etc/wolf/cfg/wolf.sock";
                            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                            var endpoint = new UnixDomainSocketEndPoint(endpointPath);
                            await socket.ConnectAsync(endpoint, token);
                            return new NetworkStream(socket, ownsSocket: true);
                        },
                        PooledConnectionLifetime = Timeout.InfiniteTimeSpan
                    };
                })
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("http://localhost/api/v1/");
                    client.DefaultRequestVersion = new Version(1, 0);
                })
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
            
            
            services.AddSingleton<WolfApiEventsTask>()
                  .AddSingleton<IApiEventSubscriber>(p => p.GetRequiredService<WolfApiEventsTask>())
                  .AddHostedService(p => p.GetRequiredService<WolfApiEventsTask>());
            
            
            var api = new NSwagWolfApi.NSwagWolfApi(new System.Net.Http.HttpClient(new SocketsHttpHandler
            {
                ConnectCallback = async (ctx, token) =>
                {
                    var endpointPath = System.Environment.GetEnvironmentVariable("WOLF_SOCKET_PATH") ?? "/etc/wolf/cfg/wolf.sock";
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    var endpoint = new UnixDomainSocketEndPoint(endpointPath);
                    await socket.ConnectAsync(endpoint, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            }));
            services.AddSingleton(api);

            var docker = new NSwagDocker.NSwagDocker(new System.Net.Http.HttpClient(new SocketsHttpHandler
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
            }));
            services.AddSingleton(docker);
            
            services.AddHostedService<Test>();
        }
    }
}