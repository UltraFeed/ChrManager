using ChrManager.Application.Models;

namespace ChrManager.Application;

internal interface IBrowserProcesses
{
	public void RequestClose (string binary);
	public Task ForceTerminateAsync (string binary, CancellationToken token);
	public bool IsRunning (string binary);
	public bool Start (string binary);
	public bool? IsModuleLoaded (string binary, string modulePath);
}

internal sealed record PreparedSelfUpdate (UpdateJob Job, string Path);

internal interface ISelfUpdatePreparation
{
	public Task<PreparedSelfUpdate?> PrepareAsync (LauncherSettings settings, PassSummary summary, CancellationToken token);
}

internal interface ISelfUpdateHandover
{
	public Task LaunchAsync (PreparedSelfUpdate prepared, CancellationToken token);
}
