using Godot;
using System;
using WolfUI;
using System.Net.Http;
using System.Text.Json;
using System.Net.Sockets;
using System.Threading.Tasks;
using Godot.DependencyInjection;
using Microsoft.Extensions.Configuration;
using WolfUI.Tasks;

namespace Resources.WolfAPI;

[GlobalClass]
public partial class WolfApi : Resource
{
    private static IConfiguration _config = null!;
    
    private static readonly ILogger<WolfApi> Logger = Main.GetLogger<WolfApi>();
    
    [Inject]
    public WolfApi(IConfiguration config)
    {
        _config = config;
    }
}