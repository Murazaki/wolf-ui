using Godot;
using Resources.WolfAPI;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot.DependencyInjection;
using NSwagWolfApi;
using WolfUI.Interfaces;
using WolfUI.Misc;
using WolfUI.Tasks;

namespace WolfUI;

[Tool, GlobalClass, SceneTree]
public partial class AppList : Control
{
	private readonly IApiEventPublisher _apiEvents;
	private readonly NSwagWolfApi.NSwagWolfApi _api;

	[Inject]
	public AppList(IApiEventPublisher apiEvents, NSwagWolfApi.NSwagWolfApi api)
	{
		_apiEvents = apiEvents;
		_api = api;
	}

	public event EventHandler<NSwagWolfApi.Lobby>? LobbyCreatedEvent;
    public event EventHandler<string>? LobbyStoppedEvent;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			ThemeChanged += EditorMockupReady;
			EditorMockupReady();
			return;
		}
		
		if (Main.Singleton.controllerMap is not null)
		{
			Main.Singleton.controllerMap.UsedControllerChanged += OnControllerChanged;
		}

		VisibilityChanged += RebuildAppList;
		ThemeChanged += RebuildAppList;

		_apiEvents.LobbyCreatedEvent += OnLobbyStarted;
		_apiEvents.LobbyStoppedEvent += OnLobbyStopped;
	}

	public override void _ExitTree()
	{
		_apiEvents.LobbyCreatedEvent -= OnLobbyStarted;
		_apiEvents.LobbyStoppedEvent -= OnLobbyStopped;
	}
	
	private void OnControllerChanged(ControllerMap.ControllerType  controllerType)
	{
		if (!Visible) return;
		
		// Ensure at least one element is focused when switching to controller.
		var focus = Main.Singleton.GetViewport().GuiGetFocusOwner();
		if (focus is not null || Main.Singleton.TopLayer.GetChildCount() > 0) return;

		if (AppGrid.GetChildren().Select(n => n as App).FirstOrDefault(n => n is not null) is { } ctrl)
			ctrl.GrabFocus();	
		else
			Main.Singleton.OptionsButton.GrabFocus();

	}
	
	private async void RebuildAppList()
	{
		if (!Visible)
		{
			Main.Singleton.BackHint.Hide();
			return;
		}

		AppGrid.Columns = AppGrid.GetThemeConstant("columns", "AppListGrid").Between(1, 6);
		Main.Singleton.BackHint.Visible = true;
		await LoadAppList();

		
		//var lobbies = await WolfApi.GetLobbies();
		var lobbies = await _api.LobbiesAsync().Lobbies();
		lobbies.ForEach(OnLobbyStarted);

		if (AppGrid.GetChildren().Select(n => n as App).FirstOrDefault(n => n is not null) is { } ctrl)
			ctrl.GrabFocus();	
		else
			Main.Singleton.OptionsButton.GrabFocus();
	}

	private void OnLobbyStopped(string lobbyId)
	{
		if (!Visible)
			return;

		LobbyStoppedEvent?.Invoke(this, lobbyId);
	}

	private void OnLobbyStarted(NSwagWolfApi.Lobby lobby)
	{
		
	}
	
	private void OnLobbyStarted(LobbyCreatedEvent? lobby)
	{
		if(lobby == null) return;
		OnLobbyStarted(lobby.ToLobby());
		if (!Visible) return;

		if (lobby?.StartedByProfileId != Main.ActiveProfile.ProfileData.Id &&
		    lobby?.StartedByProfileId != Main.ActiveProfile.ProfileData.Id) return;

		LobbyCreatedEvent?.Invoke(this, lobby.ToLobby());
	}

	public override void _Process(double delta)
	{
		if (!Visible) return;
		
		if (Engine.IsEditorHint())
		{
			return;
		}

		if (!InputActions.IsActionJustPressed("ui_select") || Main.Singleton.UserList is not Control userList) return;
		userList.Visible = true;
		SoundEffects.PlayAcceptSound();
	}

	private async Task LoadAppList()
	{
		Main.Singleton.OptionsButton.Visible = true;
		Main.Singleton.HeaderLabel.Text = "Loading...";
		
		
		foreach (var child in AppGrid.GetChildren())
		{
			child.QueueFree();
			AppGrid.RemoveChild(child);
		}
		
		// var enumerator = (await WolfApi.GetApps(WolfApi.ActiveProfile))
		// 	.Select((value, i) => (value, i));

		(await _api.GetApps(Main.ActiveProfile.ProfileData.Id!))
			.Select((a, i) => a.ToGodotNode($"App {i}"))
			.ForEach(AddAppEntry);
		
		var firstChildren = AppGrid.GetChildren()[..AppGrid.Columns].OfType<App>();
		foreach(var child in firstChildren)
		{
			child.AppButton.FocusEntered += () =>
			{
				AppScrollContainer.ScrollVertical = 0;
			};
		};
		
		var remainder = AppGrid.GetChildCount() % AppGrid.Columns;
		var idx = AppGrid.GetChildCount() - (remainder == 0 ? AppGrid.Columns : remainder);
		var lastChildren = AppGrid.GetChildren()[idx..].OfType<App>();
		foreach(var child in lastChildren)
		{
			child.AppButton.FocusEntered += () =>
			{
				AppScrollContainer.ScrollVertical = (int)AppScrollContainer.GetChildren().Cast<Control>().First().Size.X;
			};
		};
		
		Main.Singleton.HeaderLabel.Text = "Select Application";
	}

	private void EditorMockupReady()
	{
		
		AppGrid.Columns = AppGrid.GetThemeConstant("columns", "AppListGrid").Between(1, 6);
		foreach (var child in AppGrid.GetChildren())
			child.QueueFree();

		var scene = ResourceLoader.Load<PackedScene>("uid://chspw2lt1qcuc");
		for (var i = 0; i < 6; i++)
		{
			for (var j = 0; j < AppGrid.Columns; j++)
			{
				AppGrid.AddChild(scene.Instantiate());
			}
		}
	}

	private static Control BuildSpacer()
	{
		return new Control()
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
	}

	private void AddAppEntry(App newApp)
	{
		if (AppGrid is not { } gridContainer) return;
		var appEntryCount = gridContainer.GetChildCount();
		var gridColumns = gridContainer.Columns;

		gridContainer.AddChild(newApp);

		if (appEntryCount < gridColumns) return;
		var aboveApp = gridContainer.GetChild<App>(appEntryCount - gridColumns);
		newApp.FocusNeighborTop = aboveApp.GetPath();

		if (appEntryCount % gridColumns != 0) return;
		var app = gridContainer.GetChild<App>(appEntryCount - 1);
		app.FocusNeighborRight = gridContainer.GetChild<App>(-1).GetPath();
		gridContainer.GetChild<App>(-1).FocusNeighborLeft = app.GetPath();
	}
}
