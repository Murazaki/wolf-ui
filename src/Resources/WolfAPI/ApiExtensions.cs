using NSwagWolfApi;
using WolfUI;

namespace Resources.WolfAPI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using WolfUI;

    public static class ApiExtensions
    {
        public static async Task<ICollection<NSwagWolfApi.App>> Apps(
            this Task<NSwagWolfApi.AppListResponse> response)
        {
            var result = await response;
            return result.Apps;
        }

        public static async Task<ICollection<NSwagWolfApi.Profile>> Profiles(
            this Task<NSwagWolfApi.ProfileListResponse> response)
        {
            var result = await response;
            return result.Profiles;
        }

        public static async Task<ICollection<NSwagWolfApi.Lobby>> Lobbies(
            this Task<NSwagWolfApi.LobbiesResponse> response)
        {
            var result = await response;
            return result.Lobbies;
        }

        public static async Task<ICollection<NSwagWolfApi.App>> GetApps(
            this Task<NSwagWolfApi.Profile> profile)
        {
            var result = await profile;
            return result.Apps;
        }

        public static async Task<ICollection<NSwagWolfApi.App>> GetApps(
            this Task<NSwagWolfApi.AppListResponse> response)
        {
            var result = await response;
            return result.Apps;
        }

        public static async Task<ICollection<NSwagWolfApi.App>> GetApps(
            this NSwagWolfApi.NSwagWolfApi api, NSwagWolfApi.Profile profile)
        {
            var profiles = await api.ProfilesAsync();
            var apps = profiles?.Profiles
                .FirstOrDefault(p => p.Id == profile.Id)?
                .Apps ?? [];
            return apps;
        }

        public static async Task<ICollection<NSwagWolfApi.App>> GetApps(
            this NSwagWolfApi.NSwagWolfApi api, string profileId)
        {
            var profiles = await api.ProfilesAsync();
            var apps = profiles?.Profiles
                .FirstOrDefault(p => p.Id == profileId)?
                .Apps ?? [];
            return apps;
        }

        public static async Task<App> ToGodotNode(this Task<NSwagWolfApi.App> appDto)
        {
            var dto = await appDto;
            return dto.ToGodotNode();
        }

        public static App ToGodotNode(this NSwagWolfApi.App dto)
        {
            var node = App.Instantiate(dto);
            // node.Title = dto.Title;
            // node.Id = dto.Id;
            // node.IconPngPath = dto.Icon_png_path;
            // node.StartVirtualCompositor = dto.Start_virtual_compositor;
            return node;
        }

        public static App ToGodotNode(this NSwagWolfApi.App dto, string nodeName)
        {
            var node = dto.ToGodotNode();
            node.Name = nodeName;
            return node;
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }

        public static async Task<StreamSession?> GetSession(this NSwagWolfApi.NSwagWolfApi api)
        {
            var sessions = await api.SessionsAsync();
            var currSession = sessions?.Sessions
                .FirstOrDefault(s => s.Client_id == Environment.GetEnvironmentVariable("WOLF_SESSION_ID"));
            if (currSession is not null) return currSession;

            return new StreamSession
            {
                Video_width = 1920,
                Video_height = 1080,
                Video_refresh_rate = 60,
                Audio_channel_count = 2,
                Client_settings = null
            };
        }
    }
}

namespace NSwagWolfApi
{
    using Godot;
    using System.Collections.Generic;
    using Resources.WolfAPI;
    
    public partial class App
    {
        public string? GetIconPath()
        {
            string icon;
            if (Icon_png_path is null)
            {
                if (Runner.Image is null || !Runner.Image.Contains("ghcr.io/games-on-whales/"))
                    return null;

                var name = Runner.Image.TrimPrefix("ghcr.io/games-on-whales/");
                var idx = name.LastIndexOf(':');
                if (idx >= 0)
                    name = name[..idx];

                icon = $"https://games-on-whales.github.io/wildlife/apps/{name}/assets/icon.png";
            }
            else
            {
                icon = Icon_png_path;
            }
            return icon;
        }
    }
    
    public partial class CreateLobbyRequest
    {
        public static CreateLobbyRequest FromApp(App app, 
            StreamSession session, bool multiUser = false,
            bool stopWhenEveryoneLeaves = false,
            ICollection<int>? pin = null,
            PartialClientSettings? partialClientSettings = null)
        {
            var obj = new CreateLobbyRequest()
            {
                Profile_id = Main.ActiveProfile.ProfileData.Id,
                Name = app.Title,
                Multi_user = multiUser,
                Icon_png_path = app.GetIconPath(),
                Stop_when_everyone_leaves = stopWhenEveryoneLeaves,
                Runner_state_folder = $"profile-data/{Main.ActiveProfile.ProfileData.Id}/{app.Runner.Name}",
                Runner = app.Runner,
                Video_settings = new VideoSettings()
                {
                    Width = session.Video_width,
                    Height = session.Video_height,
                    Refresh_rate = session.Video_refresh_rate,
                    Runner_render_node = app.Render_node,
                    Wayland_render_node = app.Render_node,
                    Video_producer_buffer_caps = System.Environment.GetEnvironmentVariable("WOLF_VIDEO_BUFFER_CAPS") ?? ""
                },
                Audio_settings = new AudioSettings()
                {
                    Channel_count = session.Audio_channel_count
                },
                Client_settings = new PartialClientSettings()
            };
            return obj;
        }
    }
}