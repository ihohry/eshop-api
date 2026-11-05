using System.Net.Http.Headers;
using System.Net.Http.Json;
using EShop.Api.Catalog;

namespace EShop.Api.IntegrationTests;

internal static class ImageTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static byte[] For(string extension) => extension switch
    {
        "jpg" => Build([0xFF, 0xD8, 0xFF, 0xE0]),
        "png" => Build([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        "webp" => Build([0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]),
        _ => throw new ArgumentOutOfRangeException(nameof(extension)),
    };

    private static byte[] Build(byte[] signature)
    {
        var bytes = new byte[64];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    public static async Task<AdminProductResponse> CreateProductAsync(HttpClient admin)
    {
        var suffix = Guid.NewGuid().ToString("N");

        using var categoryResponse = await admin.PostAsJsonAsync(
            "/admin/categories", new CategoryCreateRequest("Test category", $"cat-{suffix}", null), Ct);
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<AdminCategoryResponse>(Ct)
            ?? throw new InvalidOperationException("The category response was empty.");

        using var productResponse = await admin.PostAsJsonAsync(
            "/admin/products", new ProductCreateRequest(category.Id, "Test product", $"prod-{suffix}", null, 9.99m), Ct);
        productResponse.EnsureSuccessStatusCode();
        return await productResponse.Content.ReadFromJsonAsync<AdminProductResponse>(Ct)
            ?? throw new InvalidOperationException("The product response was empty.");
    }

    public static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid productId, byte[] bytes, string? altText = null)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "photo.jpg");
        if (altText is not null)
        {
            using var alt = new StringContent(altText);
            form.Add(alt, "altText");
            return await client.PostAsync($"/admin/products/{productId}/images", form, Ct);
        }

        return await client.PostAsync($"/admin/products/{productId}/images", form, Ct);
    }
}