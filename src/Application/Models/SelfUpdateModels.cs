namespace ChrManager.Application.Models;

internal sealed record SelfRelease (string Version, string Rid, string Url, string Sha256);

internal interface ISelfUpdateSource
{
	public Task<SelfRelease?> LatestAsync (CancellationToken token);
}

internal sealed record UpdateJob (string Root, string ExecutableName, int ParentId, long ParentStarted, string Nonce, string Version, string Sha256, PassSummary Summary, int ExitDelaySeconds, bool HandshakeRequired = false)
{
	public int FormatVersion { get; init; } = 1;
	public string? PreviousSha256 { get; init; }
	public string? Language { get; init; }
}

internal sealed record UpdateProgress (SelfUpdatePhase Phase, string? Error = null)
{
	public int Version { get; init; } = 1;
}
