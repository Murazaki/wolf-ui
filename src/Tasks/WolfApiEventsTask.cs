using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Godot.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Resources.WolfAPI;
using WolfUI.Interfaces;
using WolfUI.Misc;
using HttpClient = System.Net.Http.HttpClient;

namespace WolfUI.Tasks;

public partial class WolfApiEventsTask : GodotObject, IHostedService, IApiEventPublisher
{
    private readonly Microsoft.Extensions.Logging.ILogger<WolfApiEventsTask> _logger;
    private readonly HttpClient _client;
    // private readonly NSwagDocker.NSwagDocker _docker;
    private readonly NSwagWolfApi.NSwagWolfApi _wolfApi;
    public readonly ConcurrentDictionary<string, bool> ExistingDockerImages = new();

    public static readonly JsonSerializerOptions JsonOptions = new(){
        TypeInfoResolver = new OptInJsonTypeInfoResolver()
    };
    
    public WolfApiEventsTask(Microsoft.Extensions.Logging.ILogger<WolfApiEventsTask> logger, 
        IHostApplicationLifetime applicationLifetime, 
        // NSwagDocker.NSwagDocker docker, 
        NSwagWolfApi.NSwagWolfApi wolfApi, 
        HttpClient client)
    {
        _logger = logger;
        // _docker = docker;
        _wolfApi = wolfApi;
        _client = client;

        if(Engine.IsEditorHint()) return;
        
        applicationLifetime.ApplicationStopping.Register(() => 
            DependencyInjection.GetRootNode().PropagateNotification((int)Node.NotificationWMCloseRequest));
        
        applicationLifetime.ApplicationStopped.Register(() => 
            (Engine.GetMainLoop() as SceneTree)?.Quit());
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            DockerPulledImageEvent += (image, success) =>
            {
                if (success)
                {
                    ExistingDockerImages[image] = success;
                }
            };
            
            var icons = TryAsync(_wolfApi.ProfilesAsync(cancellationToken)).Result?.Profiles
                .SelectMany(p => p.Apps)
                .Distinct()
                .Select( a => (a.Id, new Lazy<Texture2D?>(() => WolfApi.GetIcon(a).Result)))
                .ToList() ?? [];
            
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var stream = await _client.GetStreamAsync($"http://localhost/api/v1/events", cancellationToken);
                    var eventType = string.Empty;
                    using var reader = new StreamReader(stream);
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        switch (line)
                        {
                            case null:
                            case ":keepalive":
                                continue;
                        }

                        if (line.StartsWith("event:"))
                            eventType = line["event: ".Length..];

                        if (!line.StartsWith("data:")) continue;
                        
                        var data = line["data: ".Length..];

                        CallDeferred(MethodName.FilterApiEvents, eventType, data);
                    }

                    _logger.LogError("Lost connection to the Wolf API SSE. End of Stream.");
                    await Task.Delay(1000, cancellationToken);
                }
                catch (HttpRequestException e)
                {
                    CallDeferred(MethodName.ApiConnectionFailed, e.Message);
                    var tokenSource = new CancellationTokenSource();
                    await Task.FromCanceled(tokenSource.Token);
                }
            }

            return;

            async Task<T?> TryAsync<T>(Task<T> func)
            {
                try { return await func; }
                catch (Exception e) { return default(T); }
            }
        }, CancellationToken.None);

        //void EmitSignalApiEventDeferred(string eventType, string data) => CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.ApiEvent, eventType, data);
        return Task.CompletedTask;
    }

    private async void ApiConnectionFailed(string msg)
    {
        _logger.LogError("Failed connecting to the Wolf API: {msg}.", msg);
        await QuestionDialogue.OpenDialogue(
            "Error", 
            $"Failed connecting to the Wolf API:\n {msg}.",
            new Dictionary<string, bool>
            {
                { "OK", true }
            });
        Main.Singleton.GetTree().Root.PropagateNotification((int)Node.NotificationWMCloseRequest);
    }
    
    private void FilterApiEvents(string @event, string data)
    {
        ApiEvent?.Invoke(@event, data);

        var operations = new Dictionary<string, Action<string>> {
            { "DockerPullImageEndEvent", InvokeDockerPulledImage},
            { "DockerPullImageStartEvent", InvokeDockerPullingImage},
            { "wolf::core::events::PlugDeviceEvent", (_)=>{}},
            { "wolf::core::events::UnplugDeviceEvent", (_)=>{}},
            { "wolf::core::events::PairSignal", (_)=>{}},
            { "wolf::core::events::StartRunner", (_)=>{}},
            { "wolf::core::events::StreamSession", (_)=>{}},
            { "wolf::core::events::StopStreamEvent", (_)=>{}},
            { "wolf::core::events::VideoSession", (_)=>{}},
            { "wolf::core::events::RTPAudioPingEvent", (_)=>{}},
            { "wolf::core::events::AudioSession", (_)=>{}},
            { "wolf::core::events::IDRRequestEvent", (_)=>{}},
            { "wolf::core::events::RTPVideoPingEvent", (_)=>{}},
            { "wolf::core::events::ResumeStreamEvent", (_)=>{}},
            { "wolf::core::events::PauseStreamEvent", (_)=>{}},
            { "wolf::core::events::SwitchStreamProducerEvents", (_)=>{}},
            { "wolf::core::events::JoinLobbyEvent", InvokeLobbyJoin},
            { "wolf::core::events::LeaveLobbyEvent", InvokeLobbyLeave},
            { "wolf::core::events::CreateLobbyEvent", InvokeLobbyCreated },
            { "wolf::core::events::StopLobbyEvent", InvokeLobbyStopped },
        };

        //var failed = delegate(){ Logger.LogInformation("{Event} - {Data}", @event, data); };

        if (!operations.TryGetValue(@event, out var value))
        {
            _logger.LogWarning("{event} - {data}", @event, data);
            return;
        }

        value(data);
        if(@event == "wolf::core::events::CreateLobbyEvent")
        {
            var a = JsonSerializer.Deserialize<NSwagWolfApi.LobbyCreatedEvent>(data, JsonOptions);
            GD.Print(a);
        }
        return;

        
        void InvokeDockerPullingImage(string dataJson) => 
            DockerPullingImageEvent?.Invoke(dataJson.TrimPrefix("{\"image_name\":\"").TrimSuffix("\"}"));
        void InvokeDockerPulledImage(string dataJson)
        {
            var image = PulledImageRegex().Replace(dataJson, @"$1");
            var success = PulledImageRegex().Replace(dataJson, @"$2") == "true";
            DockerPulledImageEvent?.Invoke(image, success);
        }
        void InvokeLobbyCreated(string dataJson) => 
            LobbyCreatedEvent?.Invoke(JsonSerializer.Deserialize<NSwagWolfApi.LobbyCreatedEvent>(dataJson, JsonOptions)!);
        void InvokeLobbyStopped(string dataJson) => 
            LobbyStoppedEvent?.Invoke(dataJson.TrimPrefix("{\"lobby_id\":\"").TrimSuffix("\"}"));
        void InvokeLobbyJoin(string dataJson) => 
            LobbyJoinEvent?.Invoke(dataJson[..dataJson.LastIndexOf(',')].TrimPrefix("{\"lobby_id\":\"").TrimSuffix("\""));
        void InvokeLobbyLeave(string dataJson) => 
            LobbyLeaveEvent?.Invoke(dataJson[..dataJson.LastIndexOf(',')].TrimPrefix("{\"lobby_id\":\"").TrimSuffix("\""));
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    public event IApiEventPublisher.ApiEventEventHandler? ApiEvent;
    public event IApiEventPublisher.LobbyCreatedEventEventHandler? LobbyCreatedEvent;
    public event IApiEventPublisher.LobbyStoppedEventEventHandler? LobbyStoppedEvent;
    public event IApiEventPublisher.LobbyJoinEventEventHandler? LobbyJoinEvent;
    public event IApiEventPublisher.LobbyLeaveEventEventHandler? LobbyLeaveEvent;
    public event IApiEventPublisher.ImageUpdatedEventHandler? ImageUpdatedEvent;
    public event IApiEventPublisher.ImageAlreadyUptoDateEventHandler? ImageAlreadyUptoDateEvent;
    public event IApiEventPublisher.ImagePullProgressEventHandler? ImagePullProgressEvent;
    public event IApiEventPublisher.DockerPullingImageEventHandler? DockerPullingImageEvent;
    public event IApiEventPublisher.DockerPulledImageEventHandler? DockerPulledImageEvent;

    [GeneratedRegex("""{"image_name":"(.*?)","success":(.*?)}""")]
    private static partial Regex PulledImageRegex();
}