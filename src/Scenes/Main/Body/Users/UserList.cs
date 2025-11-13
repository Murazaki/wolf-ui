using Godot;
using Resources.WolfAPI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.DependencyInjection;

namespace WolfUI;

[Tool, SceneTree]
public partial class UserList : Control
{
	private readonly NSwagWolfApi.NSwagWolfApi _api;

	[Inject]
	public UserList(NSwagWolfApi.NSwagWolfApi api)
	{
		_api = api;
	}

	public override async void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			EditorMockupReady();
			return;
		}

		await LoadUsers();

		Visible = true;

		VisibilityChanged += async () =>
		{
			if (Visible)
			{
				await LoadUsers();
			}
		};
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			return;
		}

		if (!Visible) return;
		var focus = Main.Singleton.GetViewport().GuiGetFocusOwner();
		if (focus is not null || Main.Singleton.TopLayer.GetChildCount() > 0) return;
		var ctrl = (Control?)UserContainer.GetChildren().ToList<Node>().Find(c => c is Control);
		ctrl?.GrabFocus();
	}

	private async Task LoadUsers()
	{
		Main.Singleton.OptionsButton.Visible = false;
		Main.Singleton.HeaderLabel.Text = "Loading...";

		foreach (var child in UserContainer.GetChildren())
			child.QueueFree();
		
		var profiles = await _api.ProfilesAsync().Profiles();

		foreach (var profile in profiles)
		{
			UserContainer.AddChild(Profile.Instantiate(profile));
		}

		var ch = UserContainer.GetChildren();
		if (ch.Count > 0 && ch[0] is Button b)
		{
			b.CallDeferred(Control.MethodName.GrabFocus);
		}
		Main.Singleton.HeaderLabel.Text = "Select User";
	}

	private void EditorMockupReady()
	{
		List<Profile> userList =
		[
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "One" }),
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Two" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Three" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Four" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Five" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Six" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Seven" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Eight" }), 
			Profile.Instantiate(new NSwagWolfApi.Profile(){ Name = "Nine" }), 
		];
		foreach(var usr in userList)
		{
			UserContainer.AddChild(usr);
		}
	}
}
