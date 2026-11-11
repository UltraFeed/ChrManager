using ChrManager.Application;
using ChrManager.Infrastructure.SelfUpdate;

namespace ChrManager.Infrastructure.Startup;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class SelfUpdateHandover : ISelfUpdateHandover
{
	public Task LaunchAsync (PreparedSelfUpdate prepared, CancellationToken token) { return SelfUpdateWorker.LaunchHelperAsync(prepared.Job, prepared.Path, token); }
}
