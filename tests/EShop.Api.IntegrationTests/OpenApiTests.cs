using System.Net;
using System.Text.Json;
using Xunit;

namespace EShop.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class OpenApiTests(ApiFactory factory)
{
    [Fact]
    public async Task OpenApi_document_is_generated()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/products", out _));
    }

    [Fact]
    public async Task Products_list_parameters_keep_their_limits()
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);

        var page = document.RootElement
            .GetProperty("paths").GetProperty("/products").GetProperty("get").GetProperty("parameters")
            .EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "page");
        Assert.Equal(1, page.GetProperty("schema").GetProperty("minimum").GetInt32());
    }
}