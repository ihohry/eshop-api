namespace EShop.Domain.Identity;

public sealed record RotatedToken(Guid UserId, string Token);

public interface IRefreshTokenService
{
    /// <summary>Starts a new family (used by login).</summary>
    Task<string> IssueAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns null when the token is unknown, expired, revoked or reused.</summary>
    Task<RotatedToken?> RotateAsync(string token, CancellationToken ct);

    Task RevokeFamilyAsync(string token, CancellationToken ct);
}