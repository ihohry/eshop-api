namespace EShop.Domain.Identity;

public enum LoginStatus
{
    Succeeded,
    InvalidCredentials,
    LockedOut,
}

public sealed class LoginResult
{
    private LoginResult(LoginStatus status, AuthenticatedUser? user)
    {
        Status = status;
        User = user;
    }

    public LoginStatus Status { get; }

    /// <summary>The authenticated user. Not null only when <see cref="Status"/> is <see cref="LoginStatus.Succeeded"/>.</summary>
    public AuthenticatedUser? User { get; }

    public static LoginResult Success(AuthenticatedUser user) => new(LoginStatus.Succeeded, user);

    public static LoginResult InvalidCredentials() => new(LoginStatus.InvalidCredentials, null);

    public static LoginResult LockedOut() => new(LoginStatus.LockedOut, null);
}