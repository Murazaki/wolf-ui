namespace WolfUI.Tasks
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Godot;
    using Microsoft.Extensions.Logging;
    
    public partial class WolfApiEventsTask
    {
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
                        ExistingDockerImages[imageName] = true;

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
                    CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.ImagePullProgress,
                        argImageName, progress);

                void EmitSignalDeferredImageAlreadyUptoDate(string argImageName) =>
                    CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.ImageAlreadyUptoDate,
                        argImageName);

                void EmitSignalDeferredImageUpdated(string argImageName) =>
                    CallDeferred(GodotObject.MethodName.EmitSignal, SignalName.ImageUpdated, argImageName);
            });
        }
        [Signal]
        public delegate void ImageUpdatedEventHandler(string imageName);
        [Signal]
        public delegate void ImageAlreadyUptoDateEventHandler(string imageName);
        [Signal]
        public delegate void ImagePullProgressEventHandler(string imageName, double progress);
    }
}