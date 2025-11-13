using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Resources.WolfAPI;
using WolfUI.Interfaces;
using WolfUI.Tasks;

namespace NSwagDocker
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    
    public partial class NSwagDocker : IDockerApiClient, IDockerEventPublisher
    {
        private readonly ILogger<NSwagDocker> _logger;
        private readonly HttpClient _client;
        private readonly NSwagWolfApi.NSwagWolfApi _wolfApi;
        private readonly ConcurrentDictionary<string, bool> _existingDockerImages = new();
        
        public NSwagDocker(ILogger<NSwagDocker> logger, 
            HttpClient client, 
            NSwagWolfApi.NSwagWolfApi wolfApi) : this(client)
        {
            _logger = logger;
            _client = client;
            _wolfApi = wolfApi;

            CheckDockerImages();
        }

        private void CheckDockerImages()
        {
            Task.Run(async () =>
            {
                var profiles = (await _wolfApi.ProfilesAsync())?.Profiles ?? [];
                profiles.SelectMany(p => p.Apps)
                    .Select(a => a.Runner.Image)
                    .Distinct()
                    .Select(i => (
                        i, 
                        TryAsync(InspectAsync(i)).Result is not null)
                    )
                    .ForEach(kv => _existingDockerImages[kv.i] = kv.Item2);
            });
        }

        private sealed record PullImageResponse
        {
            [JsonInclude, JsonPropertyName("success")]
            public bool? Success { get; init; }

            [JsonInclude, JsonPropertyName("layer_id")]
            public string? LayerId { get; init; }

            [JsonInclude, JsonPropertyName("current_progress")]
            public long CurrentProgress { get; init; }

            [JsonInclude, JsonPropertyName("total")]
            public long Total { get; init; }
        }

        public void PullImage(string imageName)
        {
            Task.Run(async () =>
            {
                var json = $$"""
                             { "image_name": "{{imageName}}" }
                             """;

                EmitSignalDeferredImagePullProgress(imageName, 0.0);

                _logger.LogInformation("Pulling image: {img}", imageName);

                var reqMsg = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/docker/images/pull")
                {
                    Content = new StringContent(json)
                };

                var response = await _client.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead);
                _logger.LogInformation("Pull request: {httpcode}", response.StatusCode);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("Cant Pull Image: {img}, {httpcode}", imageName, response.StatusCode);
                }

                var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                Dictionary<string, PullImageResponse> layerSizes = [];
                var isUnpacking = false;
                long lastCurrent = 0;
                var hasDownloaded = false;
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null)
                        continue;

                    var parsed = JsonSerializer.Deserialize<PullImageResponse>(line, WolfApiEventsTask.JsonOptions);
                    if (parsed is null)
                        continue;


                    if (parsed.Success is not null && parsed.Success == true)
                    {
                        _existingDockerImages[imageName] = true;

                        if (hasDownloaded)
                            EmitSignalDeferredImageUpdated(imageName);
                        else
                            EmitSignalDeferredImageAlreadyUptoDate(imageName);

                        _logger.LogInformation("Image: {img} {status}", imageName,
                            hasDownloaded ? "was Updated" : "is already up to Date");
                        return;
                    }

                    hasDownloaded = true;

                    if (parsed.LayerId is null)
                        continue;

                    layerSizes[parsed.LayerId] = new PullImageResponse
                    {
                        LayerId = parsed.LayerId,
                        CurrentProgress = parsed.CurrentProgress,
                        Total = parsed.Total
                    };

                    long current = 0;
                    long total = 0;

                    foreach (var kv in layerSizes)
                    {
                        current += kv.Value.CurrentProgress;
                        total += kv.Value.Total;
                    }

                    var sizeTotal = total;
                    total *= 2;

                    if (total <= 1000) continue;

                    if (lastCurrent > 0 && lastCurrent > current + 0.3 * lastCurrent)
                        isUnpacking = true;
                    lastCurrent = current;
                    var percentProgress = 100.0 * (current + (isUnpacking ? sizeTotal : 0)) / total;
                    EmitSignalDeferredImagePullProgress(imageName, percentProgress);
                }

                return;

                void EmitSignalDeferredImagePullProgress(string argImageName, double progress) =>
                    OnImagePullProgress?.Invoke(argImageName, progress);

                void EmitSignalDeferredImageAlreadyUptoDate(string argImageName) =>
                    OnImageAlreadyUptoDate?.Invoke(argImageName);

                void EmitSignalDeferredImageUpdated(string argImageName) =>
                    OnImageUpdated?.Invoke(argImageName);
            });
        }

        public Task<DockerInspect> InspectImage(string imageName)
        {
            return InspectImage(imageName, CancellationToken.None);
        }
        
        public Task<DockerInspect> InspectImage(string imageName, CancellationToken cancellationToken)
        {
            return InspectAsync(imageName, cancellationToken);
        }

        public bool IsDockerImageOnDisk(string imageName)
        {
            return _existingDockerImages.GetValueOrDefault(imageName, false);
        }

        private static async Task<T?> TryAsync<T>(Task<T> task)
        {
            try
            {
                return await task;
            }
            catch (Exception e)
            {
                return default;
            }
        }
        
        public event IDockerEventPublisher.ImageUpdatedEventHandler? OnImageUpdated;
        public event IDockerEventPublisher.ImageAlreadyUptoDateEventHandler? OnImageAlreadyUptoDate;
        public event IDockerEventPublisher.ImagePullProgressEventHandler? OnImagePullProgress;
    }
}