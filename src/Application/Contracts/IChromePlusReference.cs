using ChrManager.Application.Models;

namespace ChrManager.Application.Contracts;

internal interface IChromePlusReference
{
	public Task<ChromePlusCheck> CheckAsync (LauncherSettings settings, CancellationToken token);
}

internal sealed record ChromePlusDifference (string Folder, IReadOnlyList<string> Add, IReadOnlyList<string> Remove);
internal sealed record IgnoredChromePlusSetting (string Folder, string Key);
internal sealed record ChromePlusCheck (IReadOnlyList<ChromePlusDifference> Differences, IReadOnlyList<IgnoredChromePlusSetting> Ignored);
