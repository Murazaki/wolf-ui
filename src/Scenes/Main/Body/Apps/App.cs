using System;
using System.Collections.Generic;
using Godot;
using Godot.DependencyInjection;
using NSwagWolfApi;
using Resources.WolfAPI;
using WolfUI.Tasks;

namespace WolfUI;

[GlobalClass, Tool, SceneTree]
public partial class App : MarginContainer
{
	private readonly NSwagWolfApi.NSwagWolfApi _api;
	private readonly NSwagDocker.NSwagDocker _docker;
	private readonly WolfApiEventsTask _events;
	public NSwagWolfApi.App AppDto = null!;
	
	private enum AppState
	{
		NOT_ON_DISK = 0,
		DOWNLOADING,
		PLAYING,
		OK,
		NONE
	}
	private AppState _state = AppState.NONE;
	private AppState State
	{
		get => _state;
		set
		{
			if (_state == value) return;
			_state = value;
			OnStateChanged();
		}
	}
	private NSwagWolfApi.Lobby? _runningLobby;
	//private Resources.WolfAPI.AppEntry _appEntry;
	private bool _isImageOnDisc = true;

	[OnInstantiate(ctor: "none")]
	private void Initialise(NSwagWolfApi.App appDto)
	{
		AppDto = appDto;
	}

	[Inject]
	public App(NSwagWolfApi.NSwagWolfApi api, WolfApiEventsTask events, NSwagDocker.NSwagDocker docker)
	{
		_api = api;
		_events = events;
		_docker = docker;
	}
	
	private bool _wasInView;
	[Signal] private delegate void AppEnteredViewEventHandler();
	[Signal] private delegate void AppRunningEventHandler();
	[Signal] private delegate void AppStoppedEventHandler();

	private bool IsAlreadyRunning(NSwagWolfApi.Lobby lobby)
	{
		//check if the App.Title is the same as the lobbies Name
		if (lobby.Name == AppDto.Title) return true;
		//check if this app uses the same folder as the lobby
		if (lobby.AdditionalProperties.ContainsKey("Runner_state_folder"))
		{
			if (AppDto.Runner.Name is null || (string?)lobby.AdditionalProperties["Runner_state_folder"] ==
			    $"profile-data/{Main.ActiveProfile.ProfileData.Id}/{AppDto.Runner.Name}")
			{
				return true;
			}
		}
		// if (_appDTO.Runner.Name is null || lobby.RunnerStateFolder == $"profile-data/{WolfApi.ActiveProfile.ProfileData.Id}/{_appDTO.Runner.Name}")
		// 	return true;

		return false;
	}

	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			return;
		}

		try
		{
			_isImageOnDisc = await _docker.IsImageOnDisk(AppDto.Runner.Image);
		}
		catch (System.Net.Http.HttpRequestException e)
		{
			_Ready();
			return;
		}
		
		
		if (AppName is null)
		{
			return;
		}
		AppName.Text = AppDto.Title;
		//DownloadIcon.Hide();
		//AppProgress.Hide();
		AppButton.Pressed += () =>
		{
			AppMenu.Visible = true;
			MenuButtonStart.GrabFocus();
		};

		FocusEntered += AppMenu.Hide;
		MenuButtonCancel.Pressed += AppButton.GrabFocus; //Hides menu via the FocusEntered above
		MenuButtonUpdate.Pressed += PullImage;
		MenuButtonCoop.Pressed += OnCoopPressed;
		MenuButtonStop.Pressed += OnStopPressed;
		MenuButtonStart.Pressed += OnStartPressed;

		State = AppState.OK;

		if (Main.Singleton.AppList is { } appList)
		{
			appList.LobbyCreatedEvent += OnLobbyCreatedEvent;
			appList.LobbyStoppedEvent += OnLobbyStoppedEvent;
		}

		AppRunning += () =>
		{
			State = AppState.PLAYING;
		};

		AppStopped += () =>
		{
			State = _isImageOnDisc ? AppState.OK : AppState.NOT_ON_DISK;
		};

		_events.ImageUpdated += OnImageUpdated;
		_events.ImageAlreadyUptoDate += OnImageUpdated;
		_events.ImagePullProgress += OnImagePullProgress;

		AppEnteredView += async () =>
		{
			AppIcon.Texture = await WolfApi.GetIcon(this);
		};
	}

	public override void _ExitTree()
	{
		base._ExitTree();

		if (Engine.IsEditorHint())
		{
			return;
		}

		if (Main.Singleton.AppList is not { } appList) return;
		appList.LobbyCreatedEvent -= OnLobbyCreatedEvent;
		appList.LobbyStoppedEvent -= OnLobbyStoppedEvent;
	}

	private void OnLobbyCreatedEvent(object? caller, NSwagWolfApi.Lobby lobby)
	{
		if (!IsInstanceValid(this) || !IsAlreadyRunning(lobby)) return;
		_runningLobby = lobby;
		EmitSignalAppRunning();
	}

	private void OnLobbyStoppedEvent(object? caller, string lobbyId)
	{
		if (!IsInstanceValid(this) || lobbyId != _runningLobby?.Id) return;
		_runningLobby = null;
		EmitSignalAppStopped();
	}

	private void OnImageUpdated(string image)
	{
		if (!IsInstanceValid(this) || AppDto.Runner.Image != image) return;
		State = _runningLobby is null ? AppState.OK : AppState.PLAYING;
	}

	private void OnImagePullProgress(string image, double progress)
	{
		if (AppDto.Runner.Image is null || image != AppDto.Runner.Image) return;
		if (!IsInstanceValid(ProgressBar) || !IsInstanceValid(AppButton) || !IsInstanceValid(DisabledIndicator)) return;

		State = AppState.DOWNLOADING;

		if (!ProgressBar.Visible || !AppButton.Disabled || !DisabledIndicator.Visible)
		{
			ProgressBar.Visible = true;
			AppButton.Disabled = true;
			DisabledIndicator.Visible = true;
		}

		ProgressBar.Value = progress;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (Engine.IsEditorHint())
		{
			return;
		}

		if (!_wasInView 
		    && Main.Singleton.AppList is { } appList 
		    && GetGlobalRect().Intersection(appList.GetGlobalRect()).HasArea())
		{
			EmitSignalAppEnteredView();
			_wasInView = true;
		}


		if (AppMenu.Visible && !(
				MenuButtonCancel.HasFocus() ||
				MenuButtonUpdate.HasFocus() ||
				MenuButtonCoop.HasFocus() ||
				MenuButtonStop.HasFocus() ||
				MenuButtonStart.HasFocus())
			)
		{
			AppMenu.Hide();
		}

		if (AppMenu.Visible && InputActions.IsActionJustPressed("ui_cancel"))
		{
			AppButton.GrabFocus();
		}
		
		if (AppDto.Runner.Image is null || State == AppState.DOWNLOADING) return;
		if(_events.ExistingDockerImages.TryGetValue(AppDto.Runner.Image, out var image))
			_isImageOnDisc = image;
		State = _isImageOnDisc ? _runningLobby is null ? AppState.OK : AppState.PLAYING : AppState.NOT_ON_DISK;
	}

	private void OnStateChanged()
	{
		switch (State)
		{
			case AppState.OK:
				DownloadHint.Visible = false;
				PlayingHint.Visible = false;
				OkHint.Visible = true;

				DisabledIndicator.Visible = false;
				ProgressBar.Visible = false;

				AppButton.Disabled = false;
				ProgressBar.Value = 0;

				MenuButtonStart.Text = "Start";
				MenuButtonStart.Disabled = false;
				MenuButtonStop.Visible = false;
				MenuButtonCoop.Disabled = false;
				MenuButtonUpdate.Disabled = false;

				MenuButtonStart.FocusNeighborBottom = MenuButtonCoop.GetPath();
				MenuButtonStart.FocusNext = MenuButtonCoop.GetPath();

				MenuButtonCoop.FocusPrevious = MenuButtonStart.GetPath();
				MenuButtonCoop.FocusNeighborTop = MenuButtonStart.GetPath();
				break;
			
			case AppState.PLAYING:
				DownloadHint.Visible = false;
				PlayingHint.Visible = true;
				OkHint.Visible = false;
				ProgressBar.Value = 0;
				
				if (_runningLobby is not null && !_runningLobby.Multi_user)
				{
					MenuButtonStart.Text = "Connect";
					MenuButtonStop.Visible = true;
				}

				MenuButtonCoop.Disabled = true;

				MenuButtonStart.FocusNeighborBottom = MenuButtonStop.GetPath();
				MenuButtonStart.FocusNext = MenuButtonStop.GetPath();

				MenuButtonStop.FocusPrevious = MenuButtonStart.GetPath();
				MenuButtonStop.FocusNeighborTop = MenuButtonStart.GetPath();

				MenuButtonStop.FocusNeighborBottom = MenuButtonCoop.GetPath();
				MenuButtonStop.FocusNext = MenuButtonCoop.GetPath();

				MenuButtonCoop.FocusPrevious = MenuButtonStop.GetPath();
				MenuButtonCoop.FocusNeighborTop = MenuButtonStop.GetPath();
				break;
			
			case AppState.NOT_ON_DISK:
				DownloadHint.Visible = true;
				PlayingHint.Visible = false;
				OkHint.Visible = false;
				ProgressBar.Value = 0;

				MenuButtonCoop.Disabled = true;
				MenuButtonUpdate.Disabled = true;
				MenuButtonStart.Text = "Download";

				DisabledIndicator.Visible = true;
				break;
			
			case AppState.DOWNLOADING:
				DownloadHint.Visible = true;
				PlayingHint.Visible = false;
				OkHint.Visible = false;

				ProgressBar.Visible = true;
				AppButton.Disabled = true;
				DisabledIndicator.Visible = true;
				break;
			
			case AppState.NONE:
				break;
			
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	private void OnPressed()
	{
		AppMenu.Visible = true;
		MenuButtonStart.GrabFocus();
	}
	
	private async void OnStartPressed()
	{
		//TODO: check if user already has a open singleplayer lobby for the chosen ap or folder and if yes re-join.

		if (AppDto.Runner.Name is null) return;

		MenuButtonStart.Disabled = true;

		if (!_isImageOnDisc)
		{
			PullImage();
			GrabFocus();
			return;
		}

		var lobby = _runningLobby;

		var lobbyId = lobby?.Id;
		if (lobby == null)
		{
			var session = await _api.GetSession();
			if (session?.Client_settings is null)
				return;

			var createLobbyRequest = CreateLobbyRequest.FromApp(AppDto, session);
			
			var response = await _api.CreateAsync(createLobbyRequest);
			if (response is { Success: true })
			{
				lobbyId = response.Lobby_id;
			}
		}

		State = AppState.PLAYING;
		
		if (lobbyId is not null)
		{
			var response = await _api.JoinAsync(new JoinLobbyEvent()
			{
				Lobby_id = lobbyId,
				Moonlight_session_id = 0
			});
			if (!response.Success)
			{
				await QuestionDialogue.OpenDialogue("Lobby full", "The Lobby you tried joining is Full.", new Dictionary<string, bool>()
				{
					{"OK", true}
				});
			}
		}
		
		MenuButtonStart.Disabled = false;

		AppButton.GrabFocus();
	}

	private async void OnStopPressed()
	{
		if (_runningLobby?.Id is null)
			return;
		
		MenuButtonStop.Disabled = true;
		//await WolfApi.StopLobby(_runningLobby.Id);
		await _api.StopAsync(new StopLobbyEvent()
		{
			Lobby_id = _runningLobby.Id
		});
		MenuButtonStop.Disabled = false;

		State = _isImageOnDisc ? AppState.NOT_ON_DISK : AppState.OK;

		AppButton.GrabFocus();
	}

	public new void GrabFocus()
	{
		AppButton.GrabFocus();
	}

	private async void OnCoopPressed()
	{
		if (AppDto.Runner.Name is null)
			return;

		MenuButtonCoop.Disabled = true;

		var session = await _api.GetSession();
		if (session?.Client_settings is null)
			return;

		var createLobbyRequest = CreateLobbyRequest.FromApp(AppDto, session, multiUser:true, stopWhenEveryoneLeaves:false);
		
		if (await QuestionDialogue.OpenDialogue("Pin", "Add Pin to Lobby?",
			new Dictionary<string, bool> {
				{ "Yes", true },
				{ "No", false }
			},
			new Dictionary<string, Func<bool>>
			{
				{"No", () => InputActions.IsActionJustPressed("ui_cancel") }
			}
		))
		{
			var pin = await PinInput.RequestPin();
			createLobbyRequest.Pin = pin;
		}

		var lobbyId = string.Empty;
		var response = await _api.CreateAsync(createLobbyRequest);
		if (response is { Success: true })
		{
			lobbyId = response.Lobby_id;
		}


		if (lobbyId is not null)
		{
			await _api.JoinAsync(new JoinLobbyEvent()
			{
				Lobby_id = lobbyId,
				Moonlight_session_id = 0,
				Pin = createLobbyRequest.Pin
			});
			//await _wolfApi.JoinLobby(lobbyId, lobby.Pin!);

			//await WolfApi.JoinLobby(lobbyId, WolfApi.SessionId, lobby.Pin);
		}

		MenuButtonCoop.Disabled = false;

		AppButton.GrabFocus();

		State = AppState.PLAYING;
	}

	private void PullImage()
	{
		State = AppState.DOWNLOADING;
		AppButton.GrabFocus();
		if (AppDto.Runner.Image is null) return;
		_events.PullImage(AppDto.Runner.Image);
	}
}
