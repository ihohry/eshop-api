namespace EShop.Domain.Identity;

public interface IAccountService
{
    /// <summary>Registers a new user with the Customer role.</summary>
    /// <exception cref="EShop.Domain.Exceptions.ConflictException">The email is already registered.</exception>
    Task<RegistrationResult> RegisterCustomerAsync(string email, string password);
}