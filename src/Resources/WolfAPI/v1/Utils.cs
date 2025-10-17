using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Godot;
using WolfUI;

namespace Resources.WolfAPI;

public partial class WolfApi
{
    private static readonly System.Net.Http.HttpClient HttpClient = new(new SocketsHttpHandler
    {
        ConnectCallback = async (ctx, token) =>
        {
            var endpointPath = System.Environment.GetEnvironmentVariable("WOLF_SOCKET_PATH") ?? "/etc/wolf/cfg/wolf.sock";
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            var endpoint = new UnixDomainSocketEndPoint(endpointPath);
            await socket.ConnectAsync(endpoint, token);
            return new NetworkStream(socket, ownsSocket: true);
        }
    });

    public static async Task<Texture2D?> GetIcon(NSwagWolfApi.App app, double hCacheDuration = 1.0, int retry = 0)
    {
        return await GetIcon(app.ToGodotNode(), hCacheDuration, retry);
    }
    public static async Task<Texture2D?> GetIcon(App app, double hCacheDuration = 1.0, int retry = 0)
    {
        if (app.AppDto.Icon_png_path is not null && app.AppDto.Icon_png_path != "") // image set, get image from wolf
            return await GetIcon(app.AppDto.Icon_png_path, hCacheDuration, retry);

        if (app.AppDto.Runner?.Image is null || !app.AppDto.Runner.Image.Contains("ghcr.io/games-on-whales/"))
            return null;

        var name = app.AppDto.Runner.Image.TrimPrefix("ghcr.io/games-on-whales/");//.TrimSuffix(":edge");
        var idx = name.LastIndexOf(':');
        if (idx >= 0)
            name = name[..idx];

        return await GetIcon($"https://games-on-whales.github.io/wildlife/apps/{name}/assets/icon.png", hCacheDuration, retry);
    }
    public static async Task<Texture2D?> GetIcon(string iconPath, double hCacheDuration = 1.0, int retry = 0)
    {
        if (retry >= 5)
        {
            Logger.LogError("Failed Loading {0} 5 times, skipping", iconPath);
            return null;
        }
        var user = System.Environment.GetEnvironmentVariable("USER") ?? "retro";
        user = user == "root" ? "retro" : user;

        var filepath = $"/home/{user}/.wolf-ui/tmp/icons/{iconPath}.png";
        filepath = filepath.Replace("\\", "/").Replace("//", "/");

        if (File.Exists(filepath))
        {
            if (File.GetCreationTime(filepath).AddHours(hCacheDuration).CompareTo(DateTime.Now) >= 0)
            {
                var image = Image.LoadFromFile(filepath);
                return ImageTexture.CreateFromImage(image);
            }

            File.Delete(filepath);
        }

        Logger.LogInformation("Requesting icon: {0}", iconPath);

        HttpResponseMessage message;
        try
        {
            message = await HttpClient.GetAsync($"http://localhost/api/v1/utils/get-icon?icon_path={iconPath}");
        }
        catch (HttpRequestException e)
        {
            if(e.InnerException is not null)
                Logger.LogWarning("Icon {0} could not be accessed: {1} - {2} Retrying", iconPath, e.Message, e.InnerException.Message);
            else
                Logger.LogWarning("Icon {0} could not be accessed: {1} Retrying", iconPath, e.Message);
            return await GetIcon(iconPath, hCacheDuration, retry + 1);
        }

        if (message.StatusCode == HttpStatusCode.OK)
        {
            Image image = new();
            var error = await image.LoadImageFromHttpResponseMessage(message);
            if (error != Error.Ok)
            {
                if (error == Error.FileUnrecognized)
                {
                    return null;
                }

                Logger.LogError("Icon {0} could not be decoded properly, Retrying", iconPath);
                return await GetIcon(iconPath, hCacheDuration, retry + 1);
            }

            var directoryPath = Path.GetDirectoryName(filepath);
            if (directoryPath is not null)
            {
                Directory.CreateDirectory(directoryPath);
                image.SavePng(filepath);
            }
            var texture = ImageTexture.CreateFromImage(image);
            return texture;
        }
        Logger.LogError("Could not access image: {0}: {1}", iconPath, message.StatusCode);
        return null;
    }
}