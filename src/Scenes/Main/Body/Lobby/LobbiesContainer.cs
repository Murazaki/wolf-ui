using Godot;
using Godot.DependencyInjection;
using NSwagWolfApi;
using Resources.WolfAPI;
using WolfUI.Interfaces;
using WolfUI.Tasks;

namespace WolfUI;

[Tool, SceneTree]
public partial class LobbiesContainer : VBoxContainer
{
    private static readonly ILogger<LobbiesContainer> Logger = Main.GetLogger<LobbiesContainer>();
    private readonly IApiEventPublisher _apiEvents;
    private readonly NSwagWolfApi.NSwagWolfApi _api;

    [Inject]
    public LobbiesContainer(IApiEventPublisher apiEvents, NSwagWolfApi.NSwagWolfApi api)
    {
        _apiEvents = apiEvents;
        _api = api;
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
        Logger.LogInformation("Lobby stopped {0}", lobbyId);

        foreach (var node in Lobbies.GetChildren())
        {
            if (node.Name != lobbyId) continue;
            Lobbies.RemoveChild(node);
            node.QueueFree();
        }
    }
}
