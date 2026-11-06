using System.Net;

namespace EShop.Api.IntegrationTests;

internal sealed class ClientSet : IDisposable
{
    public List<HttpClient> Clients { get; } = [];

    public void Dispose()
    {
        foreach (var client in Clients)
        {
            client.Dispose();
        }
    }
}

internal static class ConcurrencyTestData
{
    public static async Task<ClientSet> CreateCustomersAsync(ApiFactory factory, int count)
    {
        var set = new ClientSet();
        for (var i = 0; i < count; i++)
        {
            var customer = await factory.RegisterAndLoginAsync();
            set.Clients.Add(factory.CreateAuthorizedClient(customer));
        }

        return set;
    }

    public static async Task<List<HttpStatusCode>> SendAllAsync(
        IReadOnlyList<HttpClient> clients, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = clients.Select(async client =>
        {
            await gate.Task;
            using var response = await send(client);
            return response.StatusCode;
        }).ToList();

        gate.SetResult();
        return [.. await Task.WhenAll(tasks)];
    }
}