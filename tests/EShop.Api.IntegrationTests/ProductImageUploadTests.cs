using System.Net;
using System.Net.Http.Json;
using System.Text;
using EShop.Api.Catalog;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ProductImageUploadTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> CreateAdminClientAsync() =>
        factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());

    [Theory]
    [InlineData("jpg")]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task Upload_detects_the_type_from_the_file_signature(string extension)
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);

        using var response = await ImageTestData.UploadAsync(admin, product.Id, ImageTestData.For(extension));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var image = await response.Content.ReadFromJsonAsync<ProductImageResponse>(Ct);
        Assert.NotNull(image);
        Assert.EndsWith("." + extension, image.Url, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(factory.ImageRoot, Path.GetFileName(image.Url))));
    }

    [Fact]
    public async Task Upload_keeps_alt_text_and_numbers_the_positions()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);

        using var first = await ImageTestData.UploadAsync(admin, product.Id, ImageTestData.For("jpg"), "Front view");
        using var second = await ImageTestData.UploadAsync(admin, product.Id, ImageTestData.For("png"));

        var firstImage = await first.Content.ReadFromJsonAsync<ProductImageResponse>(Ct);
        var secondImage = await second.Content.ReadFromJsonAsync<ProductImageResponse>(Ct);
        Assert.NotNull(firstImage);
        Assert.NotNull(secondImage);
        Assert.Equal("Front view", firstImage.AltText);
        Assert.Equal(0, firstImage.Position);
        Assert.Null(secondImage.AltText);
        Assert.Equal(1, secondImage.Position);
    }

    [Fact]
    public async Task Upload_of_a_fake_image_returns_unprocessable_entity_and_stores_nothing()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);
        var filesBefore = Directory.GetFiles(factory.ImageRoot).Length;

        using var response = await ImageTestData.UploadAsync(
            admin, product.Id, Encoding.UTF8.GetBytes("this is not an image"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(filesBefore, Directory.GetFiles(factory.ImageRoot).Length);
    }

    [Fact]
    public async Task Upload_to_an_unknown_product_returns_not_found()
    {
        using var admin = await CreateAdminClientAsync();

        using var response = await ImageTestData.UploadAsync(admin, Guid.NewGuid(), ImageTestData.For("jpg"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_as_a_customer_returns_forbidden()
    {
        using var customer = factory.CreateAuthorizedClient(await factory.RegisterAndLoginAsync());

        using var response = await ImageTestData.UploadAsync(customer, Guid.NewGuid(), ImageTestData.For("jpg"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_without_a_token_returns_unauthorized()
    {
        using var anonymous = factory.CreateClient();

        using var response = await ImageTestData.UploadAsync(anonymous, Guid.NewGuid(), ImageTestData.For("jpg"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}