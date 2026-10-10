namespace EShop.Domain.Identity;

public enum LoginStatus
{
    Succeeded,
    InvalidCredentials,
    InvalidRefreshToken,
    LockedOut,
}

public sealed class LoginResult
{
    private LoginResult(LoginStatus status, AuthenticatedUser? user, string? refreshToken)
    {
        Status = status;
        User = user;
        RefreshToken = refreshToken;
    }

    public LoginStatus Status { get; }

    /// <summary>Not null only when <see cref="Status"/> is <see cref="LoginStatus.Succeeded"/>.</summary>
    public AuthenticatedUser? User { get; }

    /// <summary>Not null only when <see cref="Status"/> is <see cref="LoginStatus.Succeeded"/>.</summary>
    public string? RefreshToken { get; }

    public static LoginResult Success(AuthenticatedUser user, string refreshToken) =>
        new(LoginStatus.Succeeded, user, refreshToken);

    public static LoginResult InvalidCredentials() => new(LoginStatus.InvalidCredentials, null, null);

    public static LoginResult InvalidRefreshToken() => new(LoginStatus.InvalidRefreshToken, null, null);

    public static LoginResult LockedOut() => new(LoginStatus.LockedOut, null, null);
}