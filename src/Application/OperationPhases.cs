using System.Text.Json.Serialization;

namespace ChrManager.Application;

internal enum InstallationPhase
{
	[JsonStringEnumMemberName("prepared")] Prepared,
	[JsonStringEnumMemberName("old-moved")] OldMoved,
	[JsonStringEnumMemberName("new-moved")] NewMoved,
	[JsonStringEnumMemberName("committed")] Committed
}

internal enum SelfUpdatePhase
{
	[JsonStringEnumMemberName("backing-up")] BackingUp,
	[JsonStringEnumMemberName("installing")] Installing,
	[JsonStringEnumMemberName("starting")] Starting,
	[JsonStringEnumMemberName("restoring")] Restoring,
	[JsonStringEnumMemberName("complete")] Complete,
	[JsonStringEnumMemberName("failed")] Failed,
	[JsonStringEnumMemberName("rolled-back")] RolledBack,
	[JsonStringEnumMemberName("recovery-failed")] RecoveryFailed,
	[JsonStringEnumMemberName("aborted")] Aborted
}
