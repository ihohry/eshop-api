namespace EShop.Domain.Exceptions;

/// <summary>A business rule was violated.</summary>
public class DomainException(string message) : Exception(message);