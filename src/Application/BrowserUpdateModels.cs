namespace ChrManager.Application;

internal enum BrowserUpdateStatus
{
	Ready,
	Deferred,
	Failed
}

internal sealed record BrowserUpdateResult (BrowserUpdateStatus Status, Exception? Error = null);

internal sealed record PassSummary (int Succeeded, int Failed, int Deferred);

internal sealed record BrowserUpdatePlan (string BinaryPath, string Version, bool Upgrade, bool Configured, bool NoChecksum, string ModulePath);
