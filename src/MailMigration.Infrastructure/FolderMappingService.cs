using MailMigration.Application;
using MailMigration.Domain;

namespace MailMigration.Infrastructure;

public sealed class FolderMappingService : IFolderMappingService
{
    public IReadOnlyList<FolderMapping> CreateMappings(MailboxAnalysis source, MailboxAnalysis target)
    {
        var targetBySpecialUse = target.Folders.Where(x => x.SpecialUse is not null).GroupBy(x => x.SpecialUse!, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First().FullName, StringComparer.OrdinalIgnoreCase);
        var targetNames = target.Folders.ToDictionary(x => x.FullName, StringComparer.OrdinalIgnoreCase);
        return source.Folders.Select(folder =>
        {
            if (folder.SpecialUse is not null && targetBySpecialUse.TryGetValue(folder.SpecialUse, out var special)) return new FolderMapping(folder.FullName, special, folder.SpecialUse);
            if (targetNames.TryGetValue(folder.FullName, out var exact)) return new FolderMapping(folder.FullName, exact.FullName, folder.SpecialUse);
            return new FolderMapping(folder.FullName, Normalize(folder.FullName), folder.SpecialUse);
        }).ToList();
    }

    private static string Normalize(string path) => path switch
    {
        "Sent Items" or "Gönderilmiş" => "Sent",
        "Deleted Items" or "Çöp" => "Trash",
        "Taslaklar" => "Drafts",
        "Spam" => "Junk",
        "Arşiv" => "Archive",
        _ => path
    };
}
