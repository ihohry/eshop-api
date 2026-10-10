namespace EShop.Domain.Identity;

public sealed record AuthenticatedUser(Guid Id, string Email, IReadOnlyList<string> Roles);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(AuthenticatedUser user);
}