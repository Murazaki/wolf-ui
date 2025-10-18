using System.Collections.Generic;
using System.Linq;
using Godot;
using Resources.WolfAPI;

namespace WolfUI;

[Tool, SceneTree]
public partial class Profile : Button
{
    public NSwagWolfApi.Profile ProfileData { get; set; }
    
    [Signal]
    private delegate void EnteredViewEventHandler();
    private bool _wasInView = false;
    //public Profile profile;
    
    [OnInstantiate(ctor: "public")]
    private void Initialise(NSwagWolfApi.Profile profile)
    {
        Name = profile.Name;
        ProfileData = profile;
    }
    
    public override void _Ready()
    {
        NameLabel.Text = Name;
        NameLabel.Text = ProfileData.Name;
        
        if (Engine.IsEditorHint())
        {
            return;
        }

        //Profile profileNode = Profile.New(profile);
        Pressed += OnPressed;
        EnteredView += OnEnteredView;
    }

    private async void OnEnteredView()
    {
        if (ProfileData.Icon_png_path is not null && ProfileData.Icon_png_path != "")
            Icon = await WolfApi.GetIcon(ProfileData.Icon_png_path);
    }
    
    private async void OnPressed()
    {
        if (ProfileData.Pin is not null)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            var pin = await PinInput.RequestPin();
            if (!pin.SequenceEqual(ProfileData.Pin))
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
        }

        Main.ActiveProfile = this;

        if(Main.Singleton.AppList is { } appMenu)
            appMenu.Visible = true;
    }
    
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
		{
			return;
		}


        if (_wasInView || !GetGlobalRect().
                Intersection(Main.Singleton.GetNode<Control>("%UserList").GetGlobalRect())
                .HasArea()) return;
        
        EmitSignalEnteredView();
        _wasInView = true;
    }


}
