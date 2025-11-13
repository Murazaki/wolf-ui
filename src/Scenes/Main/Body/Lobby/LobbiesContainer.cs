using Godot;
using Godot.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSwagWolfApi;
using Resources.WolfAPI;
using WolfUI.Interfaces;

namespace WolfUI;

[Tool, SceneTree]
public partial class LobbiesContainer : VBoxContainer
{
    private readonly ILogger<LobbiesContainer> _logger;
    private readonly IApiEventPublisher _apiEvents;
    private readonly NSwagWolfApi.NSwagWolfApi _api;

    [Inject]
    public LobbiesContainer(IApiEventPublisher apiEvents, NSwagWolfApi.NSwagWolfApi api, ILogger<LobbiesContainer> logger)
    {
        _apiEvents = apiEvents;
        _api = api;
        _logger = logger;
    }

    public override async void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            EditorMockupReady();
            return;
        }

        Hide();

        _apiEvents.LobbyCreatedEvent += AddLobby;
        _apiEvents.LobbyStoppedEvent += OnLobbyStopped;
        Lobbies.ChildEnteredTree += (__) => SetDeferred(CanvasItem.PropertyName.Visible, true);
        Lobbies.ChildExitingTree += (__) => CallDeferred(MethodName.OnChildExitingTree);

        //var currLobbies = await WolfApi.GetLobbies();
        var currLobbies = await _api.LobbiesAsync().Lobbies();
        foreach (var lobby in currLobbies)
        {
            AddLobby(lobby);
        }
    }

    private void EditorMockupReady()
    {
        for(var i = 0; i < 10; ++i)
        {
            var node = Lobby.Instantiate();
            node.Name = $"{i}";
            Lobbies.AddChild(node);
        }
    }

    private void OnChildExitingTree()
    {
        //Logger.LogInformation("{Msg}", lobbies.GetChildCount());
        //GD.Print(lobbies.GetChildCount());
        if(Lobbies.GetChildCount() == 0)
        {
            Hide();
        }
    }

    private void AddLobby(LobbyCreatedEvent lobby)
    {
        AddLobby(lobby.ToLobby());
    }
    
    private void AddLobby(NSwagWolfApi.Lobby lobby)
    {
        if (!lobby.Multi_user)
            return;

        var node = Lobby.Instantiate(lobby);
            
        Lobbies.AddChild(node);
    }
    
    private void OnLobbyStopped(string lobbyId)
    {
        _logger.LogInformation("Lobby stopped {id}", lobbyId);

        foreach (var node in Lobbies.GetChildren())
        {
            if (node.Name != lobbyId) continue;
            Lobbies.RemoveChild(node);
            node.QueueFree();
        }
    }
}
