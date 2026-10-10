namespace EShop.Domain.Identity;

public interface IAccountService
{
    /// <summary>Registers a new user with the Customer role.</summary>
    /// <exception cref="EShop.Domain.Exceptions.ConflictException">The email is already registered.</exception>
    Task<RegistrationResult> RegisterCustomerAsync(string email, string password);

    /// <summary>Checks the credentials. Failed attempts are counted and can lock the account.</summary>
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}