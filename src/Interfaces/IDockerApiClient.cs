using System.Threading;
using System.Threading.Tasks;

namespace WolfUI.Interfaces;

public interface IDockerApiClient
{
    void PullImage(string imageName);
    Task<NSwagDocker.DockerInspect> InspectImage(string imageName);
    Task<NSwagDocker.DockerInspect> InspectImage(string imageName, CancellationToken cancellationToken);
    
    bool IsDockerImageOnDisk(string imageName);
}