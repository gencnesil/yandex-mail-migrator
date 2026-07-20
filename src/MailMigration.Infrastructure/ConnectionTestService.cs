using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailMigration.Application;
using MailMigration.Domain;

namespace MailMigration.Infrastructure;

public sealed class ConnectionTestService(ICredentialStorageService credentials) : IConnectionTestService
{
    public async Task<IReadOnlyList<ConnectionTestResult>> TestImapAsync(MailAccount account, CancellationToken cancellationToken)
    {
        var results = new List<ConnectionTestResult>();
        var timer = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(account.Imap.Host, cancellationToken);
            results.Add(new(ConnectionTestKind.Dns, true, timer.Elapsed, string.Join(", ", addresses.Select(x => x.ToString()))));
        }
        catch (Exception ex) { return [new(ConnectionTestKind.Dns, false, timer.Elapsed, string.Empty, Error: Friendly(ex))]; }

        X509Certificate? certificate = null;
        using var client = new ImapClient { Timeout = account.Imap.TimeoutSeconds * 1000 };
        client.ServerCertificateValidationCallback = (_, cert, _, errors) => { certificate = cert; return errors == System.Net.Security.SslPolicyErrors.None || account.Imap.AcceptInvalidCertificate; };
        try
        {
            timer.Restart();
            await client.ConnectAsync(account.Imap.Host, account.Imap.Port, account.Imap.Security.ToSocketOptions(), cancellationToken);
            results.Add(new(ConnectionTestKind.ImapConnection, true, timer.Elapsed, client.Capabilities.ToString(), client.SslProtocol.ToString(), Describe(certificate)));
            timer.Restart();
            var password = await credentials.GetAsync(account.CredentialKey, cancellationToken) ?? throw new InvalidOperationException("Kimlik bilgisi bulunamadı.");
            await client.AuthenticateAsync(account.Username, password, cancellationToken);
            results.Add(new(ConnectionTestKind.ImapAuthentication, true, timer.Elapsed, client.AuthenticationMechanisms.Count == 0 ? "LOGIN" : string.Join(",", client.AuthenticationMechanisms), client.SslProtocol.ToString(), Describe(certificate), "IMAP SASL/LOGIN"));
        }
        catch (Exception ex)
        {
            var kind = client.IsConnected ? ConnectionTestKind.ImapAuthentication : ConnectionTestKind.ImapConnection;
            results.Add(new(kind, false, timer.Elapsed, string.Empty, client.SslProtocol.ToString(), Describe(certificate), Error: Friendly(ex)));
        }
        finally { if (client.IsConnected) await client.DisconnectAsync(true, CancellationToken.None); }
        return results;
    }

    public async Task<IReadOnlyList<ConnectionTestResult>> TestSmtpAsync(MailAccount account, CancellationToken cancellationToken)
    {
        if (account.Smtp is null) return [];
        var timer = Stopwatch.StartNew();
        X509Certificate? certificate = null;
        using var client = new SmtpClient { Timeout = account.Smtp.TimeoutSeconds * 1000 };
        client.ServerCertificateValidationCallback = (_, cert, _, errors) => { certificate = cert; return errors == System.Net.Security.SslPolicyErrors.None || account.Smtp.AcceptInvalidCertificate; };
        try
        {
            await client.ConnectAsync(account.Smtp.Host, account.Smtp.Port, account.Smtp.Security.ToSocketOptions(), cancellationToken);
            var results = new List<ConnectionTestResult> { new(ConnectionTestKind.SmtpConnection, true, timer.Elapsed, client.Capabilities.ToString(), client.SslProtocol.ToString(), Describe(certificate), Critical: false) };
            timer.Restart();
            var password = await credentials.GetAsync(account.CredentialKey, cancellationToken) ?? throw new InvalidOperationException("Kimlik bilgisi bulunamadı.");
            await client.AuthenticateAsync(account.Username, password, cancellationToken);
            results.Add(new(ConnectionTestKind.SmtpAuthentication, true, timer.Elapsed, "SMTP AUTH başarılı", client.SslProtocol.ToString(), Describe(certificate), "SMTP SASL", Critical: false));
            return results;
        }
        catch (Exception ex) { return [new(client.IsConnected ? ConnectionTestKind.SmtpAuthentication : ConnectionTestKind.SmtpConnection, false, timer.Elapsed, string.Empty, client.SslProtocol.ToString(), Describe(certificate), Error: Friendly(ex), Critical: false)]; }
        finally { if (client.IsConnected) await client.DisconnectAsync(true, CancellationToken.None); }
    }

    private static string? Describe(X509Certificate? certificate) => certificate is null ? null : $"{certificate.Subject}; {certificate.GetCertHashString()}";
    private static string Friendly(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";
}
