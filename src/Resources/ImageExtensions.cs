using System.Net.Http;
using System.Threading.Tasks;
using Godot;

namespace Resources.WolfAPI;

public static class ImageExtensions
{
    public static async Task<Error> LoadImageFromHttpResponseMessage(this Image image, HttpResponseMessage message)
    {
        if (message.Content.Headers.ContentType?.MediaType is null)
        {
            return Error.DoesNotExist;
        }

        var mediaType = message.Content.Headers.ContentType.MediaType;
        if (mediaType is null)
            return default;

        var error = mediaType switch
        {
            "image/png" => image.LoadPngFromBuffer(await message.Content.ReadAsByteArrayAsync()),
            _ => Error.FileUnrecognized
        };

        if (error == Error.FileUnrecognized)
        {
            GD.Print($"No Load function for \"{mediaType}\" currently implemented");
        }

        return error;
    }

}
