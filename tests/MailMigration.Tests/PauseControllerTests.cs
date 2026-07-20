using MailMigration.Infrastructure;

namespace MailMigration.Tests;

public sealed class PauseControllerTests
{
    [Fact]
    public async Task Wait_blocks_until_resume()
    {
        var controller = new PauseController(); controller.Pause();
        var waiting = controller.WaitIfPausedAsync(default);
        Assert.False(waiting.IsCompleted);
        controller.Resume(); await waiting;
        Assert.False(controller.IsPaused);
    }
}
