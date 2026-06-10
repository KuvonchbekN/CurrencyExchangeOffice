using System.Security.Cryptography;
using System.Text;

namespace CurrencyExchangeOffice.Service.Data;

public class PasswordHasher
{
    public string CreateSalt()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    public string HashPassword(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(salt + password);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public bool Verify(string password, string salt, string expectedHash)
    {
        var actualHash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedHash));
    }
}
