using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using EShop.Domain.Identity;
using EShop.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EShop.Infrastructure.Identity;

internal sealed class RefreshTokenService(
    AppDbContext db, TimeProvider time, IOptions<JwtOptions> options) : IRefreshTokenService
{
    public async Task<string> IssueAsync(Guid userId, CancellationToken ct)
    {
        var (raw, entity) = Create(userId, Guid.NewGuid());
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<RotatedToken?> RotateAsync(string token, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hash = Hash(token);
        var current = await db.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (current is null) return null;

        if (current.RevokedAt is not null)
        {
            // Rotated before = reuse (possible theft). Logout-revoked = plain rejection.
            if (current.ReplacedByTokenId is not null)
                await RevokeFamilyAsync(current.FamilyId, now, ct);
            return null;
        }

        if (current.ExpiresAt <= now) return null;

        var (raw, next) = Create(current.UserId, current.FamilyId);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, next.Id), ct);

        if (updated == 0)
        {
            await tx.RollbackAsync(ct);
            await RevokeFamilyAsync(current.FamilyId, now, ct);
            return null;
        }

        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new RotatedToken(current.UserId, raw);
    }

    public async Task RevokeFamilyAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var familyId = await db.RefreshTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(ct);
        if (familyId is not null)
            await RevokeFamilyAsync(familyId.Value, time.GetUtcNow(), ct);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    private (string Raw, RefreshToken Entity) Create(Guid userId, Guid familyId)
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var now = time.GetUtcNow();
        return (raw, new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.Value.RefreshTokenDays),
        });
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}