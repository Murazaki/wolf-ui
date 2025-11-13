using Godot;
using Godot.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Resources.WolfAPI;

[GlobalClass]
public partial class WolfApi : Resource
{
    private static IConfiguration _config = null!;
    private static Microsoft.Extensions.Logging.ILogger<WolfApi> _logger = null!;
    private static WolfApi _instance;
    
    [Inject]
    private WolfApi(IConfiguration config, Microsoft.Extensions.Logging.ILogger<WolfApi> logger)
    {
        _config = config;
        _logger = logger;
    }

    static WolfApi()
    {
        _instance = new WolfApi();
    }
}