namespace WolfUI.Interfaces;

public interface IDockerEventPublisher
{
    public delegate void ImageUpdatedEventHandler(string imageName);
    event ImageUpdatedEventHandler OnImageUpdated;
    
    public delegate void ImageAlreadyUptoDateEventHandler(string imageName);
    event ImageAlreadyUptoDateEventHandler OnImageAlreadyUptoDate;
    
    public delegate void ImagePullProgressEventHandler(string imageName, double progress);
    event ImagePullProgressEventHandler OnImagePullProgress;
}