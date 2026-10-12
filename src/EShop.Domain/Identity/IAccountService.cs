namespace EShop.Domain.Identity;

public interface IAccountService
{
    /// <summary>Registers a new user with the Customer role.</summary>
    /// <exception cref="EShop.Domain.Exceptions.ConflictException">The email is already registered.</exception>
    Task<RegistrationResult> RegisterCustomerAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Checks the credentials. Failed attempts are counted and can lock the account.</summary>
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    /// <summary>Confirms the email. Returns false if the user or the token is invalid or expired.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token);
    /// <summary>Sends a new confirmation email. Does nothing for an unknown or already confirmed email.</summary>
    Task ResendConfirmationAsync(string email, CancellationToken ct = default);
}