using MailMigration.Application;

namespace MailMigration.Infrastructure;

public sealed class PauseController : IPauseController
{
    private volatile TaskCompletionSource<bool> gate = CompletedGate();
    public bool IsPaused { get; private set; }
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public void Resume() { IsPaused = false; gate.TrySetResult(true); }
    public Task WaitIfPausedAsync(CancellationToken cancellationToken) => IsPaused ? gate.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
    private static TaskCompletionSource<bool> CompletedGate() { var value = new TaskCompletionSource<bool>(); value.SetResult(true); return value; }
}
