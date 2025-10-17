using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NSwagWolfApi
{
    public class LobbyCreatedEvent
    {
        [JsonInclude, JsonPropertyName("profile_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProfileId { get; set; }
        [JsonInclude, JsonPropertyName("started_by_profile_id"), 
         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StartedByProfileId { get; set; }
        [JsonInclude, JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }
        [JsonInclude, JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonInclude, JsonPropertyName("icon_png_path")]
        public string? IconPngPath { get; set; }
        [JsonInclude, JsonPropertyName("pin_required")]
        public bool? PinRequired { get; set; }
        public bool IsPinLocked => Pin is not null || (PinRequired is not null && PinRequired == true);
        [JsonInclude, JsonPropertyName("pin")] public List<int>? Pin { get; set; }
        [JsonInclude, JsonPropertyName("multi_user")]
        public bool MultiUser { get; set; }
        [JsonInclude, JsonPropertyName("stop_when_everyone_leaves")]
        public bool StopWhenEveryoneLeaves { get; set; }
        [JsonInclude, JsonPropertyName("runner_state_folder")]
        public string? RunnerStateFolder { get; set; }
        [JsonInclude, JsonPropertyName("runner")]
        public Runner? Runner { get; set; }
        [JsonInclude, JsonPropertyName("client_settings"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ClientSettings? ClientSettings { get; set; }
        [JsonInclude, JsonPropertyName("video_settings"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OldVideoSettings? VideoSettings { get; set; }
        [JsonInclude, JsonPropertyName("audio_settings"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OldAudioSettings? AudioSettings { get; set; }
        [JsonInclude, JsonPropertyName("connected_sessions")]
        public List<string>? ConnectedSessions { get; set; }
    }

    public class OldVideoSettings
    {
        [JsonInclude, JsonPropertyName("width")]
        public int Width { get; set; }
        [JsonInclude, JsonPropertyName("height")]
        public int Height { get; set; }
        [JsonInclude, JsonPropertyName("refresh_rate")]
        public int RefreshRate { get; set; }
        [JsonInclude, JsonPropertyName("wayland_render_node")]
        public string? WaylandRenderNode { get; set; }
        [JsonInclude, JsonPropertyName("runner_render_node")]
        public string? RunnerRenderNode { get; set; }
        [JsonInclude, JsonPropertyName("video_producer_buffer_caps")]
        public string? VideoProducerBufferCaps { get; set; }
    }
    
    public class OldAudioSettings
    {
        [JsonInclude, JsonPropertyName("channel_count")]
        public int ChannelCount {get;set;}
    }
    
    public class Runner
    {
        [JsonInclude, JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonInclude, JsonPropertyName("base_create_json"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BaseCreateJson { get; set; }
        [JsonInclude, JsonPropertyName("devices"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Devices { get; set; }
        [JsonInclude, JsonPropertyName("env"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Env { get; set; }
        [JsonInclude, JsonPropertyName("image"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Image { get; set; }
        [JsonInclude, JsonPropertyName("mounts"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Mounts { get; set; }
        [JsonInclude, JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
        [JsonInclude, JsonPropertyName("ports"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Ports { get; set; }
        [JsonInclude, JsonPropertyName("parent_session_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ParentSessionId { get; set; }
        [JsonInclude, JsonPropertyName("run_cmd"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RunCmd { get; set; }
    }
    
    public class ClientSettings
    {
        [JsonInclude, JsonPropertyName("controllers_override")]
        public List<string>? ControllersOverride {get;set;}
        [JsonInclude, JsonPropertyName("h_scroll_acceleration")]
        public float HScrollAcceleration {get;set;}
        [JsonInclude, JsonPropertyName("mouse_acceleration")]
        public float MouseAcceleration {get;set;}
        [JsonInclude, JsonPropertyName("run_gid")]
        public int RunGid {get;set;}
        [JsonInclude, JsonPropertyName("run_uid")]
        public int RunUid {get;set;}
        [JsonInclude, JsonPropertyName("v_scroll_acceleration")]
        public float VScrollAcceleration {get;set;}
    }
    
    public static class LobbyCreatedEventExtensions
    {
        public static Lobby ToLobby(this LobbyCreatedEvent lobbyCreatedEvent)
        {
            var lobby = new Lobby()
            {
                Connected_sessions = lobbyCreatedEvent.ConnectedSessions,
                Icon_png_path = lobbyCreatedEvent.IconPngPath,
                Name = lobbyCreatedEvent.Name,
                Id = lobbyCreatedEvent.Id,
                Multi_user = lobbyCreatedEvent.MultiUser,
                Pin_required = lobbyCreatedEvent.IsPinLocked,
                Runner = new Wolf__config__AppDocker__tagged()
                {
                    Base_create_json = lobbyCreatedEvent.Runner?.BaseCreateJson,
                    Devices = lobbyCreatedEvent.Runner?.Devices,
                    Env = lobbyCreatedEvent.Runner?.Env,
                    Name = lobbyCreatedEvent.Runner?.Name,
                    Image = lobbyCreatedEvent.Runner?.Image,
                    Ports = lobbyCreatedEvent.Runner?.Ports,
                    Mounts = lobbyCreatedEvent.Runner?.Mounts,
                    Type = lobbyCreatedEvent.Runner?.Type,
                },
                Started_by_profile_id = lobbyCreatedEvent.StartedByProfileId,
                Stop_when_everyone_leaves = lobbyCreatedEvent.StopWhenEveryoneLeaves,
                AdditionalProperties = new Dictionary<string, object>()
                {
                    {"Pin", lobbyCreatedEvent.Pin},
                    {"Runner_state_folder", lobbyCreatedEvent.RunnerStateFolder},
                    {"ClientSettings", lobbyCreatedEvent.ClientSettings},
                    {"VideoSettings", lobbyCreatedEvent.VideoSettings},
                    {"AudioSettings", lobbyCreatedEvent.AudioSettings},
                }
            };

            return lobby;
        }
    }
}