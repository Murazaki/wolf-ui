using Godot;
using Resources.WolfAPI;
using System.Collections.Generic;
using System.Linq;
using Godot.DependencyInjection;
using NSwagWolfApi;
using WolfUI.Tasks;

//TODO Add User counter, Add check if Lobby is empty on Stop and if not ask again.
namespace WolfUI;

[Tool, SceneTree]
public partial class Lobby : Control
{
    [Signal]
    private delegate void LobbyEnteredViewEventHandler();
    private bool _wasInView;
    private NSwagWolfApi.Lobby _lobby = null!;
    
    private readonly IApiEventSubscriber _apiEventSubscriber;
    private readonly NSwagWolfApi.NSwagWolfApi _api;

    [OnInstantiate(ctor: "none")]
    private void Initialise(NSwagWolfApi.Lobby? lobby = null)
    {
        if (lobby?.Id is null || lobby.Name is null)
            return;

        _lobby = lobby;
        Name = lobby.Id;
        AppNameLabel.Text = lobby.Name;
        CreatorNameLabel.Text = lobby.Started_by_profile_id ?? "";
    }

    [Inject]
    public Lobby(IApiEventSubscriber apiEventSubscriber, NSwagWolfApi.NSwagWolfApi api)
    {
        _apiEventSubscriber = apiEventSubscriber;
        _api = api;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;
        
        LobbyMainButton.Pressed += OpenLobbySubMenu;
        LobbyMainButton.FocusEntered += () => LobbyMenu?.Hide();

        CloseButton.Pressed += LobbyMainButton.GrabFocus;
        JoinButton.Pressed += JoinLobby;
        StopButton.Pressed += StopLobby;
        
        LobbyMenu?.Hide();
        
        PlayerCountLabel.Text = _lobby.Connected_sessions?.Count.ToString() ?? "1";

        _apiEventSubscriber.LobbyJoinEvent += OnJoinLobby;
        _apiEventSubscriber.LobbyLeaveEvent += OnLeaveLobby;
        
        LobbyEnteredView += async () =>
        {
            if (_lobby.Icon_png_path is not null && _lobby.Icon_png_path != "")
                LobbyMainButton.Icon = await WolfApi.GetIcon(_lobby.Icon_png_path);
        };
    }

    public override void _ExitTree()
    {
        _apiEventSubscriber.LobbyJoinEvent -= OnJoinLobby;
        _apiEventSubscriber.LobbyLeaveEvent -= OnLeaveLobby;
    }
    
    private void OnJoinLobby(string lobbyId)
    {
        GD.Print(lobbyId);
        if(lobbyId != _lobby?.Id) return;
        PlayerCountLabel.Text = $"{int.Parse(PlayerCountLabel.Text) + 1}";
    }
    
    private void OnLeaveLobby(string lobbyId)
    {
        if(lobbyId != _lobby?.Id) return;
        PlayerCountLabel.Text = $"{int.Parse(PlayerCountLabel.Text) - 1}";
    }
    
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        var list = (UserList)Main.Singleton.UserList;
        if (_wasInView || !GetGlobalRect().Intersection(list.GetGlobalRect()).HasArea()) return;
        EmitSignalLobbyEnteredView();
        _wasInView = true;
    }

    private void OpenLobbySubMenu()
    {
        LobbyMenu.Visible = true;
        JoinButton.GrabFocus();
    }

    private async void JoinLobby()
    {
        List<int>? pin = null;

        if (_lobby.Pin_required)
        {
            pin = await PinInput.RequestPin();
        }

        //var error = await WolfApi.JoinLobby(Name, WolfApi.SessionId, pin);
        //var error = await _wolfApi.JoinLobby(Name, pin);
        var sucess = await _api.JoinAsync(new JoinLobbyEvent()
        {
            Lobby_id = _lobby.Id,
            Pin = pin
        });
        if (sucess is null || sucess.Success) return;

        await QuestionDialogue.OpenDialogue($"Error", $"Could not Join Lobby:.", new Dictionary<string, bool>()
        {
            {"OK", true}
        });
    }

    private async void StopLobby()
    {
        var profiles = await _api.ProfilesAsync().Profiles();
        
        //var profiles = await WolfApi.GetProfiles();
        var owner = profiles
            .FirstOrDefault(profile => profile.Id == _lobby.Started_by_profile_id);
        
        var lobbies = await _api.LobbiesAsync().Lobbies();
        var lobby = lobbies.FirstOrDefault(lobby => lobby.Id == _lobby.Id);

        if (lobby is not null && owner?.Pin is not null && !lobby.Pin_required)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            await QuestionDialogue.OpenDialogue(
                "Pin required",
                "Please enter the Profiles access Pin",
                new Dictionary<string, bool>
                {
                    {"OK", false}
                });
            var pin = await PinInput.RequestPin();
            if (!pin.SequenceEqual(owner.Pin))
            {
                await QuestionDialogue.OpenDialogue(
                    "Incorrect Pin",
                    "The entered Pin is incorrect",
                    new Dictionary<string, bool>
                    {
                        {"OK", false}
                    });
                focus.GrabFocus();
                return;
            }

            await _api.StopAsync(new StopLobbyEvent()
            {
                Lobby_id = _lobby.Id,
            });
            //await WolfApi.StopLobby(Name);
        }
        else if (lobby is not null && lobby.Pin_required)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            await QuestionDialogue.OpenDialogue<bool>(
                "Pin required",
                "Please enter the Lobby Pin",
                new Dictionary<string, bool>
                {
                    {"OK", false}
                });
            var pin = await PinInput.RequestPin();
            if(IsInstanceValid(focus))
                focus.GrabFocus();
            await _api.StopAsync(new StopLobbyEvent()
            {
                Lobby_id = _lobby.Id,
                Pin = pin
            });
        }
        else
        {
            await _api.StopAsync(new StopLobbyEvent()
            {
                Lobby_id = _lobby.Id,
            });
        }
    }
}