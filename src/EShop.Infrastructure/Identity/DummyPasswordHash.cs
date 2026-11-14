using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Identity;

public sealed class DummyPasswordHash(IOptions<PasswordHasherOptions> options)
{
    private readonly Lazy<string> value = new(() =>
        new PasswordHasher<ApplicationUser>(options)
            .HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    public string Value => value.Value;
}