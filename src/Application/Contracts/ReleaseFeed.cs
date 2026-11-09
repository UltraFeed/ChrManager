using ChrManager.Application.Models;

namespace ChrManager.Application.Contracts;

internal interface IReleaseFeed
{
	public Task<IReadOnlyList<Build>> GetBuildsAsync (CancellationToken token);
}
