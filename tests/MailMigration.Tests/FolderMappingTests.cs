using MailMigration.Domain;
using MailMigration.Infrastructure;

namespace MailMigration.Tests;

public sealed class FolderMappingTests
{
    [Fact]
    public void Maps_turkish_special_folders_to_target_special_use()
    {
        var source = new MailboxAnalysis([new("Gönderilmiş", 4, 100, 1, "Sent"), new("Çöp", 2, 50, 1, "Trash")], 6, 150, null, null);
        var target = new MailboxAnalysis([new("Sent", 0, 0, 2, "Sent"), new("Trash", 0, 0, 2, "Trash")], 0, 0, null, null);
        var mappings = new FolderMappingService().CreateMappings(source, target);
        Assert.Contains(mappings, x => x.SourcePath == "Gönderilmiş" && x.TargetPath == "Sent");
        Assert.Contains(mappings, x => x.SourcePath == "Çöp" && x.TargetPath == "Trash");
    }

    [Fact]
    public void Keeps_unicode_nested_folder_name_when_target_is_missing()
    {
        var source = new MailboxAnalysis([new("Müşteriler/İstanbul", 1, 20, 1, null)], 1, 20, null, null);
        var mappings = new FolderMappingService().CreateMappings(source, new([], 0, 0, null, null));
        Assert.Equal("Müşteriler/İstanbul", Assert.Single(mappings).TargetPath);
    }
}
