namespace WolfUI.Interfaces;

public interface IApiEventPublisher
{
    public delegate void ApiEventEventHandler(string eventType, string data);
    event ApiEventEventHandler ApiEvent;
    
    public delegate void LobbyCreatedEventEventHandler(NSwagWolfApi.LobbyCreatedEvent lobby);
    event LobbyCreatedEventEventHandler LobbyCreatedEvent;
    
    public delegate void LobbyStoppedEventEventHandler(string lobbyId);
    event LobbyStoppedEventEventHandler LobbyStoppedEvent;
    
    public delegate void LobbyJoinEventEventHandler(string lobbyId);
    event LobbyJoinEventEventHandler LobbyJoinEvent;

    public delegate void LobbyLeaveEventEventHandler(string lobbyId);
    event LobbyLeaveEventEventHandler LobbyLeaveEvent;
    
    public delegate void ImageUpdatedEventHandler(string imageName);
    event ImageUpdatedEventHandler ImageUpdatedEvent;
    
    public delegate void ImageAlreadyUptoDateEventHandler(string imageName);
    event ImageAlreadyUptoDateEventHandler ImageAlreadyUptoDateEvent;

    public delegate void ImagePullProgressEventHandler(string imageName, double progress);
    event ImagePullProgressEventHandler ImagePullProgressEvent;
    
    public delegate void DockerPullingImageEventHandler(string image);
    event DockerPullingImageEventHandler DockerPullingImageEvent;
    
    public delegate void DockerPulledImageEventHandler(string image, bool success);
    event DockerPulledImageEventHandler DockerPulledImageEvent;
}