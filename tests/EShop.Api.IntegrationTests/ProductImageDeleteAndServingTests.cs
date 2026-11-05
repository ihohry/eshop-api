using System.Net;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ProductImageDeleteAndServingTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> CreateAdminClientAsync() =>
        factory.CreateAuthorizedClient(await factory.RegisterAdminAndLoginAsync());

    private string FilePathOf(string url) => Path.Combine(factory.ImageRoot, Path.GetFileName(url));

    [Fact]
    public async Task Delete_removes_the_image_and_its_file_and_keeps_the_others()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);
        var first = await ImageTestData.UploadSucceededAsync(admin, product.Id, ImageTestData.For("jpg"));
        var second = await ImageTestData.UploadSucceededAsync(admin, product.Id, ImageTestData.For("png"));

        using var response = await admin.DeleteAsync($"/admin/products/{product.Id}/images/{first.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(File.Exists(FilePathOf(first.Url)));
        Assert.True(File.Exists(FilePathOf(second.Url)));
    }

    [Fact]
    public async Task Delete_of_an_unknown_image_returns_not_found()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);

        using var response = await admin.DeleteAsync($"/admin/products/{product.Id}/images/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_through_another_product_returns_not_found_and_keeps_the_file()
    {
        using var admin = await CreateAdminClientAsync();
        var owner = await ImageTestData.CreateProductAsync(admin);
        var other = await ImageTestData.CreateProductAsync(admin);
        var image = await ImageTestData.UploadSucceededAsync(admin, owner.Id, ImageTestData.For("jpg"));

        using var response = await admin.DeleteAsync($"/admin/products/{other.Id}/images/{image.Id}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(FilePathOf(image.Url)));
    }

    [Fact]
    public async Task Delete_as_a_customer_returns_forbidden()
    {
        using var customer = factory.CreateAuthorizedClient(await factory.RegisterAndLoginAsync());

        using var response = await customer.DeleteAsync($"/admin/products/{Guid.NewGuid()}/images/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_without_a_token_returns_unauthorized()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.DeleteAsync($"/admin/products/{Guid.NewGuid()}/images/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_image_is_served_anonymously_with_cache_and_security_headers()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);
        var bytes = ImageTestData.For("jpg");
        var image = await ImageTestData.UploadSucceededAsync(admin, product.Id, bytes);
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync($"/images/{Path.GetFileName(image.Url)}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl.Public);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl.MaxAge);
        Assert.Contains(response.Headers.CacheControl.Extensions, e => e.Name == "immutable");
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task Public_image_of_an_unknown_file_returns_not_found()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync($"/images/{Guid.NewGuid():N}.jpg", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Public_image_is_gone_after_delete()
    {
        using var admin = await CreateAdminClientAsync();
        var product = await ImageTestData.CreateProductAsync(admin);
        var image = await ImageTestData.UploadSucceededAsync(admin, product.Id, ImageTestData.For("jpg"));
        using var anonymous = factory.CreateClient();

        using var deleted = await admin.DeleteAsync($"/admin/products/{product.Id}/images/{image.Id}", Ct);
        using var response = await anonymous.GetAsync($"/images/{Path.GetFileName(image.Url)}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}