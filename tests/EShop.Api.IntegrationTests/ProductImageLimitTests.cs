using System.Net;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ProductImageLimitTests(ApiFactory factory)
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int MaxImagesPerProduct = 10;
    private const int MaxAltTextLength = 200;

    private async Task<HttpClient> CreateAdminClientAsync() =>
        factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());

    [Fact]
    public async Task Upload_accepts_a_file_of_exactly_the_maximum_size()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);

        using var response = await ImageTestData.UploadAsync(
            admin, product.Id, ImageTestData.WithSize(MaxFileSizeBytes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_file_above_the_maximum_size_and_stores_nothing()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);
        var filesBefore = Directory.GetFiles(factory.ImageRoot).Length;

        using var response = await ImageTestData.UploadAsync(
            admin, product.Id, ImageTestData.WithSize(MaxFileSizeBytes + 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(filesBefore, Directory.GetFiles(factory.ImageRoot).Length);
    }

    [Fact]
    public async Task Upload_accepts_the_tenth_image_and_rejects_the_eleventh()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);

        for (var i = 0; i < MaxImagesPerProduct; i++)
        {
            await ImageTestData.UploadSucceededAsync(admin, product.Id, ImageTestData.For("jpg"));
        }

        var filesBefore = Directory.GetFiles(factory.ImageRoot).Length;

        using var response = await ImageTestData.UploadAsync(admin, product.Id, ImageTestData.For("jpg"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(filesBefore, Directory.GetFiles(factory.ImageRoot).Length);
    }

    [Fact]
    public async Task Upload_accepts_alt_text_of_the_maximum_length()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);

        using var response = await ImageTestData.UploadAsync(
            admin, product.Id, ImageTestData.For("jpg"), new string('a', MaxAltTextLength));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_alt_text_above_the_maximum_length_and_stores_nothing()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);
        var filesBefore = Directory.GetFiles(factory.ImageRoot).Length;

        using var response = await ImageTestData.UploadAsync(
            admin, product.Id, ImageTestData.For("jpg"), new string('a', MaxAltTextLength + 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(filesBefore, Directory.GetFiles(factory.ImageRoot).Length);
    }
}