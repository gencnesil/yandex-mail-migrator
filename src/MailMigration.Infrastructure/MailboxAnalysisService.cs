using MailKit;
using MailKit.Net.Imap;
using MailMigration.Application;
using MailMigration.Domain;

namespace MailMigration.Infrastructure;

public sealed class MailboxAnalysisService(ICredentialStorageService credentials) : IMailboxAnalysisService
{
    public async Task<MailboxAnalysis> AnalyzeAsync(MailAccount account, CancellationToken cancellationToken)
    {
        using var client = new ImapClient { Timeout = account.Imap.TimeoutSeconds * 1000 };
        client.ServerCertificateValidationCallback = (_, _, _, errors) => errors == System.Net.Security.SslPolicyErrors.None || account.Imap.AcceptInvalidCertificate;
        await client.ConnectAsync(account.Imap.Host, account.Imap.Port, account.Imap.Security.ToSocketOptions(), cancellationToken);
        await client.AuthenticateAsync(account.Username, await credentials.GetAsync(account.CredentialKey, cancellationToken) ?? throw new InvalidOperationException("Kimlik bilgisi bulunamadı."), cancellationToken);
        var infos = new List<MailboxFolderInfo>();
        foreach (var ns in client.PersonalNamespaces)
        {
            var root = client.GetFolder(ns);
            await VisitAsync(root, infos, cancellationToken);
        }
        await client.DisconnectAsync(true, cancellationToken);
        return new(infos, infos.Sum(x => x.MessageCount), infos.Sum(x => x.EstimatedBytes), null, null);
    }

    private static async Task VisitAsync(IMailFolder parent, List<MailboxFolderInfo> infos, CancellationToken cancellationToken)
    {
        foreach (var folder in await parent.GetSubfoldersAsync(false, cancellationToken))
        {
            if ((folder.Attributes & FolderAttributes.NoSelect) == 0)
            {
                await folder.StatusAsync(StatusItems.Count | StatusItems.Size | StatusItems.UidValidity, cancellationToken);
                infos.Add(new(folder.FullName, folder.Count, (long)(folder.Size ?? 0), folder.UidValidity, SpecialUse(folder.Attributes)));
            }
            if ((folder.Attributes & FolderAttributes.HasNoChildren) == 0) await VisitAsync(folder, infos, cancellationToken);
        }
    }

    private static string? SpecialUse(FolderAttributes attributes)
    {
        if (attributes.HasFlag(FolderAttributes.Inbox)) return "Inbox";
        if (attributes.HasFlag(FolderAttributes.Sent)) return "Sent";
        if (attributes.HasFlag(FolderAttributes.Drafts)) return "Drafts";
        if (attributes.HasFlag(FolderAttributes.Trash)) return "Trash";
        if (attributes.HasFlag(FolderAttributes.Junk)) return "Junk";
        if (attributes.HasFlag(FolderAttributes.Archive)) return "Archive";
        return null;
    }
}
