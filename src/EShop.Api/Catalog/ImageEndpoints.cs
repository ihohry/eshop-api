using EShop.Domain.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EShop.Api.Catalog;

public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/images/{fileName}", GetImage)
            .WithTags("Images")
            .AllowAnonymous();

        return app;
    }

    private static Results<FileStreamHttpResult, NotFound> GetImage(
        string fileName, IImageStorage storage, HttpContext http)
    {
        var contentType = ContentTypeFor(fileName);
        if (contentType is null)
        {
            return TypedResults.NotFound();
        }

        var stream = storage.OpenRead(fileName);
        if (stream is null)
        {
            return TypedResults.NotFound();
        }

        // File names are random and never reused, so the content never changes.
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.Stream(stream, contentType);
    }

    private static string? ContentTypeFor(string fileName) => Path.GetExtension(fileName) switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => null,
    };
}