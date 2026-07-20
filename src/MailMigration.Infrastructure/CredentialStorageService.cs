using System.Security.Cryptography;
using System.Text;
using MailMigration.Application;

namespace MailMigration.Infrastructure;

public sealed class CredentialStorageService : ICredentialStorageService
{
    private readonly string directory;
    public CredentialStorageService()
    {
        directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MailMigrationDesktop", "credentials");
        Directory.CreateDirectory(directory);
    }

    public async Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        var clear = Encoding.UTF8.GetBytes(secret);
        try
        {
            var protectedBytes = ProtectedData.Protect(clear, Encoding.UTF8.GetBytes(key), DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(PathFor(key), protectedBytes, cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var clear = ProtectedData.Unprotect(protectedBytes, Encoding.UTF8.GetBytes(key), DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }

    private string PathFor(string key) => Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");
}
