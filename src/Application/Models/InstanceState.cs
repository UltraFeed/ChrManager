namespace ChrManager.Application.Models;

internal sealed record PendingPackage (string Identity, string File, string Sha256, string Version, string BuildId, long ActualSize = 0);

internal sealed record BrowserMemory
{
	public string? InstalledVersion { get; init; }
	public string? BuildId { get; init; }
	public PendingPackage? Pending { get; init; }
	public string? ChromePlusHash { get; init; }
	public string? ConfigurationHash { get; init; }
	public InstallJournal? Journal { get; init; }
}

internal sealed record InstallJournal (InstallationPhase Phase, string Version, string BuildId, string? ChromePlusHash, string ConfigurationHash, bool HadOld);

internal sealed record RunSnapshot
{
	public int Version { get; init; } = 1;
	public DateTimeOffset? LastFeedAttempt { get; init; }
	public DateTimeOffset? LastFeedSuccess { get; init; }
	public IReadOnlyList<Build> Feed { get; init; } = [];
	public Dictionary<string, BrowserMemory> Browsers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
	public DateTimeOffset? ChromePlusAttempt { get; init; }
	public List<ReleaseAsset> ChromePlusAssets { get; init; } = [];
	public DateTimeOffset? LastSelfUpdateAttempt { get; init; }
	public string? FailedSelfUpdateVersion { get; init; }
}
