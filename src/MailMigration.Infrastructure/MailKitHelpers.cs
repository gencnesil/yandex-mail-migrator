using MailKit.Security;
using MailMigration.Domain;

namespace MailMigration.Infrastructure;

internal static class MailKitHelpers
{
    public static SecureSocketOptions ToSocketOptions(this ConnectionSecurity security) => security switch
    {
        ConnectionSecurity.None => SecureSocketOptions.None,
        ConnectionSecurity.Auto => SecureSocketOptions.Auto,
        ConnectionSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        ConnectionSecurity.StartTls => SecureSocketOptions.StartTls,
        ConnectionSecurity.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
        _ => throw new ArgumentOutOfRangeException(nameof(security))
    };
}
