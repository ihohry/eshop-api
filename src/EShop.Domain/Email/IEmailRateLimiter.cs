namespace EShop.Domain.Email;

public interface IEmailRateLimiter
{
    bool TryAcquire(string email);
}