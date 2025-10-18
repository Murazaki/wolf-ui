using System.Collections.Generic;
using System.Linq;
using Resources.WolfAPI;

namespace WolfUI;

public partial class Main
{
    private async void SelfUpdateAsync()
    {
        var autoupdateEnable = (System.Environment.GetEnvironmentVariable("WOLF_UI_AUTOUPDATE") ?? "True") == "True";
        if (!autoupdateEnable) return;

        var apps = await _api.AppsAsync().GetApps();
        var wolfUi = apps.FirstOrDefault(app => app.Runner?.Image is not null
                                                        && app.Runner.Env is not null
                                                        && app.Runner.Image.Contains("wolf-ui")
                                                        && app.Runner.Env.Contains("WOLF_UI_AUTOUPDATE=True"));

        _dockerEvents.OnImageUpdated += async (img) =>
        {
            if (wolfUi?.Runner?.Image is null || img != wolfUi.Runner.Image) return;
            if (!await QuestionDialogue.OpenDialogue("Restart", "Wolf-UI has been updated, please restart.",
                    new Dictionary<string, bool>
                    {
                        { "Restart", true },
                        { "Later", false }
                    })) return;

            GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
        };
        
        if (wolfUi?.Runner?.Image is not null)
        {
            _dockerApiClient.PullImage(wolfUi.Runner.Image);
        }
    }
}
