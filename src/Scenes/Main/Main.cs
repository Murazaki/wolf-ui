using Godot;
using Godot.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WolfUI.Interfaces;

namespace WolfUI;

[GlobalClass, SceneTree]
public partial class Main : Control
{
	private readonly IConfiguration _config;
	private readonly NSwagWolfApi.NSwagWolfApi _api;
	private readonly IDockerEventPublisher _dockerEvents;
	private readonly IDockerApiClient _dockerApiClient;
	private readonly ILogger<Main> _logger;
	private readonly IHostApplicationLifetime _appLifetime;
	
	public static Profile ActiveProfile { get; set; } = null!;
	
	[Export]
	public ControllerMap? controllerMap;
	public static Main Singleton { get; private set; }

	[Inject]
	public Main(IConfiguration config, NSwagWolfApi.NSwagWolfApi api, IDockerEventPublisher dockerEvents, IDockerApiClient dockerApiClient, Microsoft.Extensions.Logging.ILogger<Main> logger, IHostApplicationLifetime appLifetime)
	{
		_config = config;
		_api = api;
		_dockerEvents = dockerEvents;
		_dockerApiClient = dockerApiClient;
		_logger = logger;
		_appLifetime = appLifetime;
		Singleton ??= this;
	}
	
	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		_appLifetime.ApplicationStopping.Register(QueueFree);
		
		SoundEffects? soundEffects = null;
		foreach (var child in GetChildren())
		{
			if (child is SoundEffects effects)
				soundEffects = effects;
		}

		soundEffects?.CallDeferred(SoundEffects.MethodName.ApplySoundEffects, this);

		var time = new Timer
		{
			WaitTime = 0.1,
			OneShot = false,
			Autostart = true
		};
		time.Timeout += () =>
		{
			soundEffects?.ApplySoundEffects(this);
		};

		AddChild(time);

		//WolfApi.Init();

		SelfUpdateAsync();

		_logger.LogInformation("This session's id: {id}", _config.GetSection("SESSION_ID").Value ?? "UNKNOWN");
	} 
	
	
	
	public override void _Input(InputEvent @event)
	{
		controllerMap?.SetController(@event);
	}
}
